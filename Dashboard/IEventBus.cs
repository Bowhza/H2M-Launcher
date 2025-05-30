namespace Dashboard;

using System;
using System.Threading.Tasks;


// 2. Define the Event Bus interface
public interface IEventBus
{
    // Subscribe using an asynchronous callback function, returning an IDisposable
    IDisposable Subscribe<TEvent>(Func<TEvent, Task> callback) where TEvent : IEvent;

    // Optional: Subscribe using a synchronous callback function, returning an IDisposable
    IDisposable Subscribe<TEvent>(Action<TEvent> callback) where TEvent : IEvent;

    // Publish an event
    Task PublishAsync<TEvent>(TEvent @event) where TEvent : IEvent;
}