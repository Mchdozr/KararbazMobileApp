using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Nereye.Api;

/// <summary>Bu uç yalnızca Development ve Testing ortamında vardır; diğer ortamlarda rota hiç oluşmaz (404).</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class DevelopmentOnlyAttribute : Attribute;

public sealed class DevelopmentOnlyConvention(bool enabled) : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        if (enabled)
        {
            return;
        }

        foreach (var controller in application.Controllers)
        {
            var hidden = controller.Attributes.OfType<DevelopmentOnlyAttribute>().Any();
            foreach (var action in controller.Actions.ToList())
            {
                if (hidden || action.Attributes.OfType<DevelopmentOnlyAttribute>().Any())
                {
                    controller.Actions.Remove(action);
                }
            }
        }
    }
}
