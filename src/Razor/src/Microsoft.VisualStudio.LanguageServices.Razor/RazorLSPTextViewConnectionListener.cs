// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Editor;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Text.Shared.Extensions;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.LanguageServices.Implementation.Extensions;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using TextSpan = Microsoft.VisualStudio.TextManager.Interop.TextSpan;

namespace Microsoft.VisualStudio.LanguageServices.Razor;

// The entire purpose of this class is to workaround quirks in Visual Studio's core editor handling. In Razor scenarios
// we can have a multitude of content types that represents a Razor file:
//
// ** Content Type Mappings **
// RazorCSharp = .NET Framework Razor editor
// RazorCoreCSharp = .NET Core Legacy Razor editor
// Razor = .NET Core Razor editor (LSP / new)
//
// Because we have these content types that are applied based on what project the user is operating in we have to workaround
// quirks on the core editor side to ensure that language services for our "Razor" content type properly get applied. For
// instance we need to set a language service ID, we need to update options and we need to hookup data tip filters for
// debugging. Typically all of this would be handled for us but due to bugs on the platform front we need to manually do this.
// That is what this classes purpose is.
[Export(typeof(ITextViewConnectionListener))]
[TextViewRole(PredefinedTextViewRoles.Document)]
[ContentType(RazorConstants.RazorLSPContentTypeName)]
internal class RazorLSPTextViewConnectionListener : ITextViewConnectionListener
{
    private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactory;
    private readonly LSPEditorFeatureDetector _editorFeatureDetector;
    private readonly IEditorOptionsFactoryService _editorOptionsFactory;
    private readonly IClientSettingsManager _editorSettingsManager;
    private readonly IVsTextManager4 _textManager;
    private readonly SVsServiceProvider _serviceProvider;

    /// <summary>
    /// Protects concurrent modifications to _activeTextViews and _textBuffer's
    /// property bag.
    /// </summary>
    private readonly object _lock = new();

    #region protected by _lock
    private readonly List<ITextView> _activeTextViews = new();

    private ITextBuffer? _textBuffer;
    #endregion

    [ImportingConstructor]
    public RazorLSPTextViewConnectionListener(
        IVsEditorAdaptersFactoryService editorAdaptersFactory,
        LSPEditorFeatureDetector editorFeatureDetector,
        IEditorOptionsFactoryService editorOptionsFactory,
        IClientSettingsManager editorSettingsManager,
        SVsServiceProvider serviceProvider)
    {
        if (editorAdaptersFactory is null)
        {
            throw new ArgumentNullException(nameof(editorAdaptersFactory));
        }

        if (editorFeatureDetector is null)
        {
            throw new ArgumentNullException(nameof(editorFeatureDetector));
        }

        if (editorOptionsFactory is null)
        {
            throw new ArgumentNullException(nameof(editorOptionsFactory));
        }

        if (editorSettingsManager is null)
        {
            throw new ArgumentNullException(nameof(editorSettingsManager));
        }

        if (serviceProvider is null)
        {
            throw new ArgumentNullException(nameof(serviceProvider));
        }

        _editorAdaptersFactory = editorAdaptersFactory;
        _editorFeatureDetector = editorFeatureDetector;
        _editorOptionsFactory = editorOptionsFactory;
        _editorSettingsManager = editorSettingsManager;
        _serviceProvider = serviceProvider;
        _textManager = (IVsTextManager4)serviceProvider.GetService(typeof(SVsTextManager));

        Assumes.Present(_textManager);
    }

    public void SubjectBuffersConnected(ITextView textView, ConnectionReason reason, IReadOnlyCollection<ITextBuffer> subjectBuffers)
    {
        if (textView is null)
        {
            throw new ArgumentNullException(nameof(textView));
        }

        var vsTextView = _editorAdaptersFactory.GetViewAdapter(textView);

        Assumes.NotNull(vsTextView);

        // In remote client scenarios there's a custom language service applied to buffers in order to enable delegation of interactions.
        // Because of this we don't want to break that experience so we ensure not to "set" a langauge service for remote clients.
        if (!_editorFeatureDetector.IsRemoteClient())
        {
            vsTextView.GetBuffer(out var vsBuffer);
            vsBuffer.SetLanguageServiceID(RazorVisualStudioWindowsConstants.RazorLanguageServiceGuid);
        }

        RazorLSPTextViewFilter.CreateAndRegister(vsTextView, _serviceProvider, textView.TextBuffer, textView);// _textBuffer!);

        if (!textView.TextBuffer.IsRazorLSPBuffer())
        {
            return;
        }

        lock (_lock)
        {
            _activeTextViews.Add(textView);

            // Initialize the user's options and start listening for changes.
            // We only want to attach the option changed event once so we don't receive multiple
            // notifications if there is more than one TextView active.
            if (!textView.TextBuffer.Properties.ContainsProperty(typeof(RazorEditorOptionsTracker)))
            {
                // We assume there is ever only one TextBuffer at a time and thus all active
                // TextViews have the same TextBuffer.
                _textBuffer = textView.TextBuffer;

                var bufferOptions = _editorOptionsFactory.GetOptions(_textBuffer);
                var viewOptions = _editorOptionsFactory.GetOptions(textView);

                Assumes.Present(bufferOptions);
                Assumes.Present(viewOptions);

                // All TextViews share the same options, so we only need to listen to changes for one.
                // We need to keep track of and update both the TextView and TextBuffer options. Updating
                // the TextView's options is necessary so 'SPC'/'TABS' in the bottom right corner of the
                // view displays the right setting. Updating the TextBuffer is necessary since it's where
                // LSP pulls settings from when sending us requests.
                var optionsTracker = new RazorEditorOptionsTracker(TrackedView: textView, viewOptions, bufferOptions);
                _textBuffer.Properties[typeof(RazorEditorOptionsTracker)] = optionsTracker;

                // Initialize TextView options. We only need to do this once per TextView, as the options should
                // automatically update and they aren't options we care about keeping track of.
                InitializeRazorTextViewOptions(_textManager, optionsTracker);

                // A change in Tools->Options settings only kicks off an options changed event in the view
                // and not the buffer, i.e. even if we listened for TextBuffer option changes, we would never
                // be notified. As a workaround, we listen purely for TextView changes, and update the
                // TextBuffer options in the TextView listener as well.
                RazorOptions_OptionChanged(null, null);
                viewOptions.OptionChanged += RazorOptions_OptionChanged;
            }
        }
    }

    public void SubjectBuffersDisconnected(ITextView textView, ConnectionReason reason, IReadOnlyCollection<ITextBuffer> subjectBuffers)
    {
        // When the TextView goes away so does the filter.  No need to do anything more.
        // However, we do need to detach from listening for option changes to avoid leaking.
        // We should switch to listening to a different TextView if the one we're listening
        // to is disconnected.
        Assumes.NotNull(_textBuffer);

        if (!textView.TextBuffer.IsRazorLSPBuffer())
        {
            return;
        }

        lock (_lock)
        {
            _activeTextViews.Remove(textView);

            // Is the tracked TextView where we listen for option changes the one being disconnected?
            // If so, see if another view is available.
            if (_textBuffer.Properties.TryGetProperty(
                typeof(RazorEditorOptionsTracker), out RazorEditorOptionsTracker optionsTracker) &&
                optionsTracker.TrackedView == textView)
            {
                _textBuffer.Properties.RemoveProperty(typeof(RazorEditorOptionsTracker));
                optionsTracker.ViewOptions.OptionChanged -= RazorOptions_OptionChanged;

                // If there's another text view we can use to listen for options, start tracking it.
                if (_activeTextViews.Count != 0)
                {
                    var newTrackedView = _activeTextViews[0];
                    var newViewOptions = _editorOptionsFactory.GetOptions(newTrackedView);
                    Assumes.Present(newViewOptions);

                    // We assume the TextViews all have the same TextBuffer, so we can reuse the
                    // buffer options from the old TextView.
                    var newOptionsTracker = new RazorEditorOptionsTracker(
                        newTrackedView, newViewOptions, optionsTracker.BufferOptions);
                    _textBuffer.Properties[typeof(RazorEditorOptionsTracker)] = newOptionsTracker;

                    newViewOptions.OptionChanged += RazorOptions_OptionChanged;
                }
            }
        }
    }

    private void RazorOptions_OptionChanged(object? sender, EditorOptionChangedEventArgs? e)
    {
        Assumes.NotNull(_textBuffer);

        if (!_textBuffer.Properties.TryGetProperty(typeof(RazorEditorOptionsTracker), out RazorEditorOptionsTracker optionsTracker))
        {
            return;
        }

        // Retrieve current space/tabs settings from from Tools->Options and update options in
        // the actual editor.
        var settings = UpdateRazorEditorOptions(_textManager, optionsTracker);

        // Keep track of accurate settings on the client side so we can easily retrieve the
        // options later when the server sends us a workspace/configuration request.
        _editorSettingsManager.Update(settings);
    }

    private static void InitializeRazorTextViewOptions(IVsTextManager4 textManager, RazorEditorOptionsTracker optionsTracker)
    {
        var langPrefs3 = new LANGPREFERENCES3[] { new LANGPREFERENCES3() { guidLang = RazorVisualStudioWindowsConstants.RazorLanguageServiceGuid } };
        if (VSConstants.S_OK != textManager.GetUserPreferences4(null, langPrefs3, null))
        {
            return;
        }

        // General options
        optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewOptions.UseVirtualSpaceName, Convert.ToBoolean(langPrefs3[0].fVirtualSpace));

        var wordWrapStyle = WordWrapStyles.None;
        if (Convert.ToBoolean(langPrefs3[0].fWordWrap))
        {
            wordWrapStyle |= WordWrapStyles.WordWrap | WordWrapStyles.AutoIndent;
            if (Convert.ToBoolean(langPrefs3[0].fWordWrapGlyphs))
            {
                wordWrapStyle |= WordWrapStyles.VisibleGlyphs;
            }
        }

        optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewOptions.WordWrapStyleName, wordWrapStyle);
        optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.LineNumberMarginName, Convert.ToBoolean(langPrefs3[0].fLineNumbers));
        optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewOptions.DisplayUrlsAsHyperlinksName, Convert.ToBoolean(langPrefs3[0].fHotURLs));
        optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewOptions.BraceCompletionEnabledOptionName, Convert.ToBoolean(langPrefs3[0].fBraceCompletion));
        optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewOptions.CutOrCopyBlankLineIfNoSelectionName, Convert.ToBoolean(langPrefs3[0].fCutCopyBlanks));

        // Scroll bar options
        optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.HorizontalScrollBarName, Convert.ToBoolean(langPrefs3[0].fShowHorizontalScrollBar));
        optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.VerticalScrollBarName, Convert.ToBoolean(langPrefs3[0].fShowVerticalScrollBar));
        optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.ShowScrollBarAnnotationsOptionName, Convert.ToBoolean(langPrefs3[0].fShowAnnotations));
        optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.ShowChangeTrackingMarginOptionName, Convert.ToBoolean(langPrefs3[0].fShowChanges));
        optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.ShowMarksOptionName, Convert.ToBoolean(langPrefs3[0].fShowMarks));
        optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.ShowErrorsOptionName, Convert.ToBoolean(langPrefs3[0].fShowErrors));
        optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.ShowCaretPositionOptionName, Convert.ToBoolean(langPrefs3[0].fShowCaretPosition));
        optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.ShowEnhancedScrollBarOptionName, Convert.ToBoolean(langPrefs3[0].fUseMapMode));
        optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.ShowPreviewOptionName, Convert.ToBoolean(langPrefs3[0].fShowPreview));
        optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.PreviewSizeOptionName, (int)langPrefs3[0].uOverviewWidth);
    }

    private static ClientSpaceSettings UpdateRazorEditorOptions(IVsTextManager4 textManager, RazorEditorOptionsTracker optionsTracker)
    {
        var insertSpaces = true;
        var tabSize = 4;

        var langPrefs3 = new LANGPREFERENCES3[] { new LANGPREFERENCES3() { guidLang = RazorVisualStudioWindowsConstants.RazorLanguageServiceGuid } };
        if (VSConstants.S_OK != textManager.GetUserPreferences4(null, langPrefs3, null))
        {
            return new ClientSpaceSettings(IndentWithTabs: !insertSpaces, tabSize);
        }

        // Tabs options
        insertSpaces = !Convert.ToBoolean(langPrefs3[0].fInsertTabs);
        tabSize = (int)langPrefs3[0].uTabSize;

        optionsTracker.ViewOptions.SetOptionValue(DefaultOptions.ConvertTabsToSpacesOptionId, insertSpaces);
        optionsTracker.ViewOptions.SetOptionValue(DefaultOptions.TabSizeOptionId, tabSize);

        // We need to update both the TextView and TextBuffer options for tabs/spaces settings. Updating the TextView
        // is necessary so 'SPC'/'TABS' in the bottom right corner of the view displays the right setting. Updating the
        // TextBuffer is necessary since it's where LSP pulls settings from when sending us requests.
        optionsTracker.BufferOptions.SetOptionValue(DefaultOptions.ConvertTabsToSpacesOptionId, insertSpaces);
        optionsTracker.BufferOptions.SetOptionValue(DefaultOptions.TabSizeOptionId, tabSize);

        return new ClientSpaceSettings(IndentWithTabs: !insertSpaces, tabSize);
    }

    private class RazorLSPTextViewFilter : IOleCommandTarget, IVsTextViewFilter
    {
        private RazorLSPTextViewFilter(
             SVsServiceProvider serviceProvider,
             ITextBuffer textBuffer,
             ITextView textView)
        {
            _serviceProvider = serviceProvider;
            _textBuffer = textBuffer;
            _textView = textView;
        }

        private SVsServiceProvider _serviceProvider;
        private ITextBuffer _textBuffer;
        private ITextView _textView;
        private IOleCommandTarget? _next;

        private IOleCommandTarget Next
        {
            get
            {
                if (_next is null)
                {
                    throw new InvalidOperationException($"{nameof(Next)} called before being set.");
                }

                return _next;
            }
            set
            {
                _next = value;
            }
        }

         public static void CreateAndRegister(IVsTextView textView, SVsServiceProvider serviceProviderr, ITextBuffer textBuffer, ITextView textView1)
        {
            var viewFilter = new RazorLSPTextViewFilter(serviceProviderr, textBuffer, textView1);
            textView.AddCommandFilter(viewFilter, out var next);

            viewFilter.Next = next;
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            var queryResult = Next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
            return queryResult;
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            var execResult = Next.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            return execResult;
        }

        public int GetWordExtent(int iLine, int iIndex, uint dwFlags, TextSpan[] pSpan) => VSConstants.E_NOTIMPL;

        public int GetDataTipText(TextSpan[] pSpan, out string pbstrText)
        {
            //pbstrText = null!;
            //return VSConstants.E_NOTIMPL;
            int hresult = GetDataTipInfo(
                    pSpan[0].iStartIndex,
                    pSpan[0].iStartLine,
                    pSpan[0].iEndIndex,
                    pSpan[0].iEndLine,
                    pSpan,
                    out pbstrText
                    );
            // "";
            //hresult = VSConstants.S_OK;
            //hresult = 282625;
            return hresult;
        }

        public static ITextBuffer? GetBufferContainingCaret(ITextView textView, string contentType = "Roslyn Languages")
        {
            var point = GetCaretPoint(textView, s => s.ContentType.IsOfType(contentType));
            return point.HasValue ? point.Value.Snapshot.TextBuffer : null;
        }

        public static SnapshotSpan? MapUpOrDownToFirstMatch(IBufferGraph bufferGraph, SnapshotSpan span, Predicate<ITextSnapshot> match)
        {
            var spans = bufferGraph.MapDownToFirstMatch(span, SpanTrackingMode.EdgeExclusive, match);
            if (!spans.Any())
            {
                spans = bufferGraph.MapUpToFirstMatch(span, SpanTrackingMode.EdgeExclusive, match);
            }

            return spans.Select(s => (SnapshotSpan?)s).FirstOrDefault();
        }

        public static SnapshotPoint? GetCaretPoint(ITextView textView, Predicate<ITextSnapshot> match)
        {
            var caret = textView.Caret.Position;
            var span = MapUpOrDownToFirstMatch(textView.BufferGraph, new SnapshotSpan(caret.BufferPosition, 0), match);
            if (span.HasValue)
            {
                return span.Value.Start;
            }
            else
            {
                return null;
            }
        }

        public int GetDataTipInfo(
            int startLine, int startIndex,
            int endLine, int endIndex,
            Microsoft.VisualStudio.TextManager.Interop.TextSpan[] pSpan, out string pbstrText)
        {
            int result = VSConstants.E_NOTIMPL;
            string pbstrTextInternal = null!;

            var razorDebugInfoService = new RazorDebugInfoService();
            if (razorDebugInfoService != null)
            {
                var subjectBuffer = GetBufferContainingCaret(_textView);
                var componentModel = (IComponentModel)_serviceProvider.GetService(typeof(SComponentModel))!;
                var vsBuffer = componentModel.GetService<IVsEditorAdaptersFactoryService>().GetBufferAdapter(_textBuffer);

                // a razor text buffer but was expecting a cs text buffer, so would returns a null TextDocument
                var textSnapshot = _textBuffer.CurrentSnapshot;
                var document = textSnapshot.GetOpenDocumentInCurrentContextWithChanges();

                // hacky way to find TextDocument
                Workspace.TryGetWorkspace(_textBuffer.AsTextContainer()!, out var workspace);
                foreach (var project in workspace!.CurrentSolution.Projects)
                {
                    foreach (var doc in project.Documents)
                    {
                        var filePath = doc.FilePath!.ToLower();
                        if (filePath.EndsWith(".g.cs") && filePath.Contains("fetchdata"))
                        {
                            document = doc;
                        }
                    }
                }

                // falls back to original code except when hovering in debug over items in the FetchData.razor file
                if (document == null)
                {
                    pbstrText = null!;
                    return VSConstants.E_NOTIMPL;
                }

                var uiThreadOperationExecutor = componentModel!.GetService<IUIThreadOperationExecutor>();
                uiThreadOperationExecutor.Execute(
                    title: "Debugger",
                    defaultDescription: "Getting DataTip text...",
                    allowCancellation: true,
                    showProgress: false,
                    action: context =>
                    {
                        var debugger = (IVsDebugger)_serviceProvider.GetService(typeof(SVsShellDebugger))!;
                        var debugMode = new DBGMODE[1];
                        var cancellationToken = context.UserCancellationToken;

                        // this textSnapshot is invalid expected one for cs text buffer but it is for razor text buffer, and the indexes are also razor indexes not cs ones
                        var span = textSnapshot.TryGetSpan(startLine, startIndex, endLine, endIndex);

                        // span here is not properly evaluated and the start index is unknown (TODO figure out)
                        var task = razorDebugInfoService.GetDataTipInfoAsync(document!, span!.Value.Start, cancellationToken);
                        WaitAndGetResult_CanCallOnBackground(task, cancellationToken);
                        var dataTipInfo = task.Result;
                        if (!dataTipInfo.IsDefault)
                        {
                            var resultSpan = dataTipInfo.Span.ToSnapshotSpan(textSnapshot);
                            var text = dataTipInfo.Text;

                            pSpan[0] = resultSpan.ToVsTextSpan();
                            // what if the below line is the main answer... see what the inputs are for roslyn
                            result = debugger.GetDataTipValue((IVsTextLines)vsBuffer, pSpan, text, out pbstrTextInternal);
                        }
                    });
            }

            pbstrText = pbstrTextInternal;
            return result;
        }

        public static T WaitAndGetResult_CanCallOnBackground<T>(Task<T> task, CancellationToken cancellationToken)
        {
            try
            {
                task.Wait(cancellationToken);
            }
            catch (AggregateException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException ?? ex).Throw();
            }

            return task.Result;
        }

        public int GetPairExtents(int iLine, int iIndex, TextSpan[] pSpan) => VSConstants.E_NOTIMPL;
    }

    private record RazorEditorOptionsTracker(ITextView TrackedView, IEditorOptions ViewOptions, IEditorOptions BufferOptions);
}
