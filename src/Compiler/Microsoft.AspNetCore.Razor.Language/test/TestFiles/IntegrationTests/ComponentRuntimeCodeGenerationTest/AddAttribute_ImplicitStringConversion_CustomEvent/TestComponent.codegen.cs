﻿// <auto-generated/>
#pragma warning disable 1591
namespace Test
{
    #line hidden
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components;
    public partial class TestComponent : global::Microsoft.AspNetCore.Components.ComponentBase
    {
        #pragma warning disable 1998
        protected override void BuildRenderTree(global::Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder)
        {
            global::__Blazor.Test.TestComponent.TypeInference.CreateMyComponent_0(__builder, 0, 1, 
#nullable restore
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
                          c

#line default
#line hidden
#nullable disable
            , 2, global::Microsoft.AspNetCore.Components.EventCallback.Factory.Create(this, 
#nullable restore
#line 2 "x:\dir\subdir\Test\TestComponent.cshtml"
             () => { }

#line default
#line hidden
#nullable disable
            ), 3, 
#nullable restore
#line 3 "x:\dir\subdir\Test\TestComponent.cshtml"
                   true

#line default
#line hidden
#nullable disable
            , 4, "str", 5, 
#nullable restore
#line 5 "x:\dir\subdir\Test\TestComponent.cshtml"
                       () => { }

#line default
#line hidden
#nullable disable
            , 6, 
#nullable restore
#line 6 "x:\dir\subdir\Test\TestComponent.cshtml"
                     c

#line default
#line hidden
#nullable disable
            );
        }
        #pragma warning restore 1998
#nullable restore
#line 8 "x:\dir\subdir\Test\TestComponent.cshtml"
       
    private MyClass<string> c = new();

#line default
#line hidden
#nullable disable
    }
}
namespace __Blazor.Test.TestComponent
{
    #line hidden
    internal static class TypeInference
    {
        public static void CreateMyComponent_0<T>(global::Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder, int seq, int __seq0, global::Test.MyClass<T> __arg0, int __seq1, global::Microsoft.AspNetCore.Components.EventCallback __arg1, int __seq2, global::System.Boolean __arg2, int __seq3, global::System.String __arg3, int __seq4, global::System.Delegate __arg4, int __seq5, global::System.Object __arg5)
        {
        __builder.OpenComponent<global::Test.MyComponent<T>>(seq);
        __builder.AddAttribute(__seq0, "MyParameter", (object)__arg0);
        __builder.AddAttribute(__seq1, "MyEvent", (object)__arg1);
        __builder.AddAttribute(__seq2, "BoolParameter", (object)__arg2);
        __builder.AddAttribute(__seq3, "StringParameter", (object)__arg3);
        __builder.AddAttribute(__seq4, "DelegateParameter", (object)__arg4);
        __builder.AddAttribute(__seq5, "ObjectParameter", (object)__arg5);
        __builder.CloseComponent();
        }
    }
}
#pragma warning restore 1591
