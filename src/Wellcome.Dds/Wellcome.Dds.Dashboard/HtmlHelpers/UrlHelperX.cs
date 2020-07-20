using Microsoft.AspNetCore.Mvc;

namespace Wellcome.Dds.Dashboard.HtmlHelpers
{
    public static class UrlHelperX
    {
        /// <summary>
        /// Generate an Url.Action that includes 'paths' element.
        /// </summary>
        /// <param name="helper">The <see cref="IUrlHelper"/>.</param>
        /// <param name="action">The name of the action method.</param>
        /// <param name="controller">The name of the controller.</param>
        /// <param name="values">An object that contains route values.</param>
        /// <param name="path">String containing path element</param>
        /// <returns>The generated URL.</returns>
        /// <remarks>If rendered using default Url.Action the paths element will be encoded.</remarks>
        public static string ActionWithPath(this IUrlHelper helper, string action, string controller, object values,
            string path)
        {
            var renderedAction = helper.Action(action, controller, values);

            return $"{renderedAction}/{path}";
        }
    }
}