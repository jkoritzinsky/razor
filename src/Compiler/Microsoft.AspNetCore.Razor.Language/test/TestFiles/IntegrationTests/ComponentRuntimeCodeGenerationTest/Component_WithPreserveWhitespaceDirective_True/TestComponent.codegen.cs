﻿// <auto-generated/>
#pragma warning disable 1591
namespace Test
{
    #line hidden
    using global::System;
    using global::System.Collections.Generic;
    using global::System.Linq;
    using global::System.Threading.Tasks;
    using global::Microsoft.AspNetCore.Components;
    public partial class TestComponent : global::Microsoft.AspNetCore.Components.ComponentBase
    {
        #pragma warning disable 1998
        protected override void BuildRenderTree(global::Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder)
        {
            __builder.AddMarkupContent(0, "\r\n");
            __builder.OpenElement(1, "ul");
            __builder.AddMarkupContent(2, "\r\n");
#nullable restore
#line 4 "x:\dir\subdir\Test\TestComponent.cshtml"
     foreach (var item in Enumerable.Range(1, 100))
    {

#line default
#line hidden
#nullable disable
            __builder.AddContent(3, "        ");
            __builder.OpenElement(4, "li");
            __builder.AddMarkupContent(5, "\r\n            ");
#nullable restore
#line (7,14)-(7,18) 24 "x:\dir\subdir\Test\TestComponent.cshtml"
__builder.AddContent(6, item);

#line default
#line hidden
#nullable disable
            __builder.AddMarkupContent(7, "\r\n        ");
            __builder.CloseElement();
            __builder.AddMarkupContent(8, "\r\n");
#nullable restore
#line 9 "x:\dir\subdir\Test\TestComponent.cshtml"
    }

#line default
#line hidden
#nullable disable
            __builder.CloseElement();
            __builder.AddMarkupContent(9, "\r\n\r\n");
        }
        #pragma warning restore 1998
    }
}
#pragma warning restore 1591
