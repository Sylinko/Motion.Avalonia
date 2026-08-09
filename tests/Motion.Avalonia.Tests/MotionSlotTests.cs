using Avalonia;

namespace Motion.Avalonia.Tests;

public sealed class MotionSlotTests
{
    [Test]
    public void ShortcutProperty_WhenExistingEndpointValueChanges_RaisesEndpointChangedOnly()
    {
        var slot = new MotionSlot { Opacity = 0.2 };
        var valueChanges = 0;
        var structureChanges = 0;
        slot.EndpointChanged += (_, _) => valueChanges++;
        slot.EndpointStructureChanged += (_, _) => structureChanges++;

        slot.Opacity = 0.6;

        Assert.Multiple(() =>
        {
            Assert.That(valueChanges, Is.EqualTo(1));
            Assert.That(structureChanges, Is.Zero);
        });
    }

    [Test]
    public void ShortcutProperty_WhenEndpointIsAddedOrRemoved_RaisesStructureChanged()
    {
        var slot = new MotionSlot();
        var valueChanges = 0;
        var structureChanges = 0;
        slot.EndpointChanged += (_, _) => valueChanges++;
        slot.EndpointStructureChanged += (_, _) => structureChanges++;

        slot.Opacity = 0.6;
        slot.ClearValue(Visual.OpacityProperty);

        Assert.Multiple(() =>
        {
            Assert.That(valueChanges, Is.Zero);
            Assert.That(structureChanges, Is.EqualTo(2));
        });
    }
}
