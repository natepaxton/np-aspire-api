using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace NpAspire.Api.DataProtection;

/// <summary>Keeps Data Protection keys in memory only; they are lost when the process stops.</summary>
public sealed class InMemoryXmlRepository : IXmlRepository
{
    private readonly Lock _lock = new();
    private readonly List<XElement> _elements = [];

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        lock (_lock)
        {
            // Copies, so callers can't change the stored keys.
            return _elements.Select(element => new XElement(element)).ToList();
        }
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);

        lock (_lock)
        {
            _elements.Add(new XElement(element));
        }
    }
}
