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
#nullable restore
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
using Microsoft.AspNetCore.Components.Web;

#line default
#line hidden
#nullable disable
    public partial class TestComponent : global::Microsoft.AspNetCore.Components.ComponentBase
    {
        #pragma warning disable 1998
        protected override void BuildRenderTree(global::Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder)
        {
            __builder.OpenElement(0, "form");
            string __formName = global::Microsoft.AspNetCore.Components.CompilerServices.RuntimeHelpers.TypeCheck<string>("myform");
            __builder.AddAttribute(1, "class", "nice");
            __builder.AddNamedEvent("onsubmit", __formName);
            __builder.OpenElement(2, "p");
#nullable restore
#line (3,8)-(3,20) 24 "x:\dir\subdir\Test\TestComponent.cshtml"
__builder.AddContent(3, DateTime.Now);

#line default
#line hidden
#nullable disable
            __builder.CloseElement();
            __builder.CloseElement();
        }
        #pragma warning restore 1998
    }
}
#pragma warning restore 1591
