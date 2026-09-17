using System.Xml.Linq;
using NpAspire.Api.DataProtection;

namespace NpAspire.Api.Tests.DataProtection;

public class InMemoryXmlRepositoryTests
{
    [Fact]
    public void GetAllElements_IsEmpty_Initially()
    {
        Assert.Empty(new InMemoryXmlRepository().GetAllElements());
    }

    [Fact]
    public void StoreElement_AddsTheElement()
    {
        var repository = new InMemoryXmlRepository();

        repository.StoreElement(new XElement("key", new XAttribute("id", "1")), "key-1");
        repository.StoreElement(new XElement("key", new XAttribute("id", "2")), "key-2");

        Assert.Equal(["1", "2"], repository.GetAllElements().Select(element => (string?)element.Attribute("id")));
    }

    [Fact]
    public void StoredElements_CannotBeChangedFromOutside()
    {
        var repository = new InMemoryXmlRepository();
        var element = new XElement("key", "original");

        repository.StoreElement(element, "key");
        element.Value = "changed by caller";
        repository.GetAllElements().Single().Value = "changed by reader";

        Assert.Equal("original", repository.GetAllElements().Single().Value);
    }

    [Fact]
    public void StoreElement_Throws_WhenElementIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new InMemoryXmlRepository().StoreElement(null!, "key"));
    }
}
