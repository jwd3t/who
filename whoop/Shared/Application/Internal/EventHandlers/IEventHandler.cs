using Cortex.Mediator.Notifications;
using whoop.Shared.Domain.Model.Events;

namespace whoop.Shared.Application.Internal.EventHandlers;

public interface IEventHandler<TEvent> : INotificationHandler<TEvent> where TEvent : IEvent
{
}
