using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace NpAspire.Api.Routing;

/// <summary>
/// Prefixes every controller route with <paramref name="prefix"/>, so controllers declare only their resource
/// (<c>[Route("users")]</c> serves <c>api/v1/users</c>). An absolute template (<c>/…</c> or <c>~/…</c>) opts out.
/// </summary>
public sealed class RoutePrefixConvention(string prefix) : IApplicationModelConvention
{
    /// <summary>Prefix for every API controller route.</summary>
    public const string Api = "api/v1";

    private readonly RouteAttribute _prefix = new(prefix);

    public void Apply(ApplicationModel application)
    {
        foreach (var selector in application.Controllers.SelectMany(controller => controller.Selectors))
        {
            selector.AttributeRouteModel = AttributeRouteModel.CombineAttributeRouteModel(
                new AttributeRouteModel(_prefix),
                selector.AttributeRouteModel);
        }
    }
}
