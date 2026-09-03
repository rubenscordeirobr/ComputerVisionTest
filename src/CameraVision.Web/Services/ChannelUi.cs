using CameraVision.Core.Entities;
using MudBlazor;

namespace CameraVision.Web.Services;

/// <summary>Presentation of the alert channels (label, icon, colour), shared by grids and dialogs.</summary>
public static class ChannelUi
{
    public static readonly IReadOnlyList<AlertChannel> All = [AlertChannel.Email, AlertChannel.WhatsApp];

    public static string Label(AlertChannel channel) =>
        channel == AlertChannel.WhatsApp ? "WhatsApp" : "E-mail";

    public static string Icon(AlertChannel channel) =>
        channel == AlertChannel.WhatsApp ? Icons.Material.Filled.Sms : Icons.Material.Filled.Email;

    public static Color ChannelColor(AlertChannel channel) =>
        channel == AlertChannel.WhatsApp ? Color.Success : Color.Primary;

    /// <summary>"e-mail" / "número de WhatsApp" — for sentences about a contact's address.</summary>
    public static string AddressNoun(AlertChannel channel) =>
        channel == AlertChannel.WhatsApp ? "número de WhatsApp" : "e-mail";
}
