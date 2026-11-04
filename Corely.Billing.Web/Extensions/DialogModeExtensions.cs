using Corely.Billing.Web.Components;

namespace Corely.Billing.Web.Extensions;

internal static class DialogModeExtensions
{
    extension(DialogMode mode)
    {
        public string Title() =>
            mode switch
            {
                DialogMode.Approve => "Approve this request?",
                DialogMode.Decline => "Decline this request?",
                _ => "Cancel your request?",
            };

        public string ConfirmText() =>
            mode switch
            {
                DialogMode.Approve => "Approve",
                DialogMode.Decline => "Decline",
                _ => "Cancel request",
            };

        public string ConfirmClass() => mode == DialogMode.Approve ? "btn-success" : "btn-danger";

        public string Verb() =>
            mode switch
            {
                DialogMode.Approve => "approve",
                DialogMode.Decline => "decline",
                _ => "cancel",
            };

        public string Consequence() =>
            mode == DialogMode.Decline
                ? "The requester is told, with the reason if you give one, and the request moves to the history below."
                : "Whoever decides requests sees it withdrawn, and it moves to the history below.";
    }
}
