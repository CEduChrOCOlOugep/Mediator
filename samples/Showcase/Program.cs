using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddMediator(options =>
{
    options.NotificationPublisherType = typeof(FireAndForgetNotificationPublisher);
});

// Ordering of pipeline behavior registrations matter!
services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(ErrorLoggerHandler<,>));
services.AddSingleton<IPipelineBehavior<Ping, Pong>, PingValidator>();

var sp = services.BuildServiceProvider();

var mediator = sp.GetRequiredService<IMediator>();

var ping = new Ping(Guid.NewGuid());
var pong = await mediator.Send(ping);
Debug.Assert(ping.Id == pong.Id);
Console.WriteLine("Got the right ID: " + (ping, pong));

ping = ping with { Id = default };
try
{
    pong = await mediator.Send(ping);
    Debug.Assert(false, "We don't expect to get here, the PingValidator should throw ArgumentException!");
}
catch (ArgumentException) // The ErrorLoggerHandler should handle the logging for this sample
{ }

var statsHandler = sp.GetRequiredService<StatsNotificationHandler>();
var (messageCount, messageErrorCount) = statsHandler.Stats;

// First Ping succeeded, second failed validation
Debug.Assert(messageCount == 2, "We sent 2 pings");
Debug.Assert(messageErrorCount == 1, "1 of them failed validation");

// int r;
// r = await mediator.Send(new TestMessage1());
// Debug.Assert(r == default);
// r = await ((Mediator.Mediator)mediator).Send(new TestMessage1());
// Debug.Assert(r == default);
// r = (int)(await mediator.Send((object)new TestMessage1()))!;
// Debug.Assert(r == default);

int expected = 0;
await foreach (var i in mediator.CreateStream(new TestStreamMessage1()))
{
    Debug.Assert(i == expected);
    expected++;
}
Debug.Assert(expected == 3);

expected = 0;
await foreach (var i in ((Mediator.Mediator)mediator).CreateStream(new TestStreamMessage1()))
{
    Debug.Assert(i == expected);
    expected++;
}
Debug.Assert(expected == 3);

expected = 0;
await foreach (var i in mediator.CreateStream((object)new TestStreamMessage1()))
{
    Debug.Assert((int)i! == expected);
    expected++;
}
Debug.Assert(expected == 3);

Console.WriteLine("Done!");

// Types used below

public sealed record Ping(Guid Id) : IRequest<Pong>;

public sealed record Pong(Guid Id);

public sealed class PingHandler : IRequestHandler<Ping, Pong>
{
    public ValueTask<Pong> Handle(Ping request, CancellationToken cancellationToken)
    {
        return new ValueTask<Pong>(new Pong(request.Id));
    }
}

public sealed class PingValidator : IPipelineBehavior<Ping, Pong>
{
    public ValueTask<Pong> Handle(
        Ping request,
        MessageHandlerDelegate<Ping, Pong> next,
        CancellationToken cancellationToken
    )
    {
        if (request is null || request.Id == default)
            throw new ArgumentException("Invalid input");

        return next(request, cancellationToken);
    }
}

public sealed record ErrorMessage(Exception Exception) : INotification;

public sealed record SuccessfulMessage() : INotification;

public sealed class ErrorLoggerHandler<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage // Constrained to IMessage, or constrain to IBaseCommand or any custom interface you've implemented
{
    private readonly IMediator _mediator;

    public ErrorLoggerHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var response = await next(message, cancellationToken);
            await _mediator.Publish(new SuccessfulMessage());
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error handling message: " + ex.Message);
            await _mediator.Publish(new ErrorMessage(ex));
            throw;
        }
    }
}

// Notification handlers are automatically added to DI container

public sealed class ErrorNotificationHandler : INotificationHandler<ErrorMessage>
{
    public ValueTask Handle(ErrorMessage error, CancellationToken cancellationToken)
    {
        // Could log to application insights or something...
        return default;
    }
}

public sealed class StatsNotificationHandler : INotificationHandler<INotification> // or any other interface deriving from INotification
{
    private long _messageCount;
    private long _messageErrorCount;

    public (long MessageCount, long MessageErrorCount) Stats => (_messageCount, _messageErrorCount);

    public ValueTask Handle(INotification notification, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _messageCount);
        if (notification is ErrorMessage)
            Interlocked.Increment(ref _messageErrorCount);
        return default;
    }
}

public sealed class GenericNotificationHandler<TNotification> : INotificationHandler<TNotification>
    where TNotification : INotification // Notification handlers will be registered as open constrained types
{
    public ValueTask Handle(TNotification notification, CancellationToken cancellationToken)
    {
        return default;
    }
}

public sealed class FireAndForgetNotificationPublisher : INotificationPublisher
{
    public async ValueTask Publish<TNotification>(
        NotificationHandlers<TNotification> handlers,
        TNotification notification,
        CancellationToken cancellationToken
    )
        where TNotification : INotification
    {
        try
        {
            await Task.WhenAll(handlers.Select(handler => handler.Handle(notification, cancellationToken).AsTask()));
        }
        catch (Exception ex)
        {
            // Notifications should be fire-and-forget, we just need to log it!
            // This way we don't have to worry about exceptions bubbling up when publishing notifications
            Console.Error.WriteLine(ex);
        }
    }
}

// csharpier-ignore-start
public sealed record TestMessage1() : IQuery<int>; public sealed record TestMessage1Handler : IQueryHandler<TestMessage1, int> { public ValueTask<int> Handle(TestMessage1 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage2() : IQuery<int>; public sealed record TestMessage2Handler : IQueryHandler<TestMessage2, int> { public ValueTask<int> Handle(TestMessage2 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage3() : IQuery<int>; public sealed record TestMessage3Handler : IQueryHandler<TestMessage3, int> { public ValueTask<int> Handle(TestMessage3 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage4() : IQuery<int>; public sealed record TestMessage4Handler : IQueryHandler<TestMessage4, int> { public ValueTask<int> Handle(TestMessage4 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage5() : IQuery<int>; public sealed record TestMessage5Handler : IQueryHandler<TestMessage5, int> { public ValueTask<int> Handle(TestMessage5 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage6() : IQuery<int>; public sealed record TestMessage6Handler : IQueryHandler<TestMessage6, int> { public ValueTask<int> Handle(TestMessage6 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage7() : IQuery<int>; public sealed record TestMessage7Handler : IQueryHandler<TestMessage7, int> { public ValueTask<int> Handle(TestMessage7 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage8() : IQuery<int>; public sealed record TestMessage8Handler : IQueryHandler<TestMessage8, int> { public ValueTask<int> Handle(TestMessage8 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage9() : IQuery<int>; public sealed record TestMessage9Handler : IQueryHandler<TestMessage9, int> { public ValueTask<int> Handle(TestMessage9 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage10() : IQuery<int>; public sealed record TestMessage10Handler : IQueryHandler<TestMessage10, int> { public ValueTask<int> Handle(TestMessage10 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage11() : IQuery<int>; public sealed record TestMessage11Handler : IQueryHandler<TestMessage11, int> { public ValueTask<int> Handle(TestMessage11 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage12() : IQuery<int>; public sealed record TestMessage12Handler : IQueryHandler<TestMessage12, int> { public ValueTask<int> Handle(TestMessage12 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage13() : IQuery<int>; public sealed record TestMessage13Handler : IQueryHandler<TestMessage13, int> { public ValueTask<int> Handle(TestMessage13 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage14() : IQuery<int>; public sealed record TestMessage14Handler : IQueryHandler<TestMessage14, int> { public ValueTask<int> Handle(TestMessage14 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage15() : IQuery<int>; public sealed record TestMessage15Handler : IQueryHandler<TestMessage15, int> { public ValueTask<int> Handle(TestMessage15 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage16() : IQuery<int>; public sealed record TestMessage16Handler : IQueryHandler<TestMessage16, int> { public ValueTask<int> Handle(TestMessage16 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage17() : IQuery<int>; public sealed record TestMessage17Handler : IQueryHandler<TestMessage17, int> { public ValueTask<int> Handle(TestMessage17 request, CancellationToken cancellationToken) => default; }

public sealed record TestStreamMessage1() : IStreamRequest<int>; public sealed record TestStreamMessage1Handler : IStreamRequestHandler<TestStreamMessage1, int> { public async IAsyncEnumerable<int> Handle(TestStreamMessage1 request, [EnumeratorCancellation] CancellationToken cancellationToken) { for (int i = 0; i < 3; i++) { await Task.Yield(); yield return i; } } }
public sealed record TestStreamMessage2() : IStreamRequest<int>; public sealed record TestStreamMessage2Handler : IStreamRequestHandler<TestStreamMessage2, int> { public async IAsyncEnumerable<int> Handle(TestStreamMessage2 request, [EnumeratorCancellation] CancellationToken cancellationToken) { for (int i = 0; i < 3; i++) { await Task.Yield(); yield return i; } } }
public sealed record TestStreamMessage3() : IStreamRequest<int>; public sealed record TestStreamMessage3Handler : IStreamRequestHandler<TestStreamMessage3, int> { public async IAsyncEnumerable<int> Handle(TestStreamMessage3 request, [EnumeratorCancellation] CancellationToken cancellationToken) { for (int i = 0; i < 3; i++) { await Task.Yield(); yield return i; } } }
public sealed record TestStreamMessage4() : IStreamRequest<int>; public sealed record TestStreamMessage4Handler : IStreamRequestHandler<TestStreamMessage4, int> { public async IAsyncEnumerable<int> Handle(TestStreamMessage4 request, [EnumeratorCancellation] CancellationToken cancellationToken) { for (int i = 0; i < 3; i++) { await Task.Yield(); yield return i; } } }
public sealed record TestStreamMessage5() : IStreamRequest<int>; public sealed record TestStreamMessage5Handler : IStreamRequestHandler<TestStreamMessage5, int> { public async IAsyncEnumerable<int> Handle(TestStreamMessage5 request, [EnumeratorCancellation] CancellationToken cancellationToken) { for (int i = 0; i < 3; i++) { await Task.Yield(); yield return i; } } }
public sealed record TestStreamMessage6() : IStreamRequest<int>; public sealed record TestStreamMessage6Handler : IStreamRequestHandler<TestStreamMessage6, int> { public async IAsyncEnumerable<int> Handle(TestStreamMessage6 request, [EnumeratorCancellation] CancellationToken cancellationToken) { for (int i = 0; i < 3; i++) { await Task.Yield(); yield return i; } } }
public sealed record TestStreamMessage7() : IStreamRequest<int>; public sealed record TestStreamMessage7Handler : IStreamRequestHandler<TestStreamMessage7, int> { public async IAsyncEnumerable<int> Handle(TestStreamMessage7 request, [EnumeratorCancellation] CancellationToken cancellationToken) { for (int i = 0; i < 3; i++) { await Task.Yield(); yield return i; } } }
public sealed record TestStreamMessage8() : IStreamRequest<int>; public sealed record TestStreamMessage8Handler : IStreamRequestHandler<TestStreamMessage8, int> { public async IAsyncEnumerable<int> Handle(TestStreamMessage8 request, [EnumeratorCancellation] CancellationToken cancellationToken) { for (int i = 0; i < 3; i++) { await Task.Yield(); yield return i; } } }
public sealed record TestStreamMessage9() : IStreamRequest<int>; public sealed record TestStreamMessage9Handler : IStreamRequestHandler<TestStreamMessage9, int> { public async IAsyncEnumerable<int> Handle(TestStreamMessage9 request, [EnumeratorCancellation] CancellationToken cancellationToken) { for (int i = 0; i < 3; i++) { await Task.Yield(); yield return i; } } }
public sealed record TestStreamMessage10() : IStreamRequest<int>; public sealed record TestStreamMessage10Handler : IStreamRequestHandler<TestStreamMessage10, int> { public async IAsyncEnumerable<int> Handle(TestStreamMessage10 request, [EnumeratorCancellation] CancellationToken cancellationToken) { for (int i = 0; i < 3; i++) { await Task.Yield(); yield return i; } } }
public sealed record TestStreamMessage11() : IStreamRequest<int>; public sealed record TestStreamMessage11Handler : IStreamRequestHandler<TestStreamMessage11, int> { public async IAsyncEnumerable<int> Handle(TestStreamMessage11 request, [EnumeratorCancellation] CancellationToken cancellationToken) { for (int i = 0; i < 3; i++) { await Task.Yield(); yield return i; } } }
public sealed record TestStreamMessage12() : IStreamRequest<int>; public sealed record TestStreamMessage12Handler : IStreamRequestHandler<TestStreamMessage12, int> { public async IAsyncEnumerable<int> Handle(TestStreamMessage12 request, [EnumeratorCancellation] CancellationToken cancellationToken) { for (int i = 0; i < 3; i++) { await Task.Yield(); yield return i; } } }
public sealed record TestStreamMessage13() : IStreamRequest<int>; public sealed record TestStreamMessage13Handler : IStreamRequestHandler<TestStreamMessage13, int> { public async IAsyncEnumerable<int> Handle(TestStreamMessage13 request, [EnumeratorCancellation] CancellationToken cancellationToken) { for (int i = 0; i < 3; i++) { await Task.Yield(); yield return i; } } }
public sealed record TestStreamMessage14() : IStreamRequest<int>; public sealed record TestStreamMessage14Handler : IStreamRequestHandler<TestStreamMessage14, int> { public async IAsyncEnumerable<int> Handle(TestStreamMessage14 request, [EnumeratorCancellation] CancellationToken cancellationToken) { for (int i = 0; i < 3; i++) { await Task.Yield(); yield return i; } } }
public sealed record TestStreamMessage15() : IStreamRequest<int>; public sealed record TestStreamMessage15Handler : IStreamRequestHandler<TestStreamMessage15, int> { public async IAsyncEnumerable<int> Handle(TestStreamMessage15 request, [EnumeratorCancellation] CancellationToken cancellationToken) { for (int i = 0; i < 3; i++) { await Task.Yield(); yield return i; } } }
public sealed record TestStreamMessage16() : IStreamRequest<int>; public sealed record TestStreamMessage16Handler : IStreamRequestHandler<TestStreamMessage16, int> { public async IAsyncEnumerable<int> Handle(TestStreamMessage16 request, [EnumeratorCancellation] CancellationToken cancellationToken) { for (int i = 0; i < 3; i++) { await Task.Yield(); yield return i; } } }
public sealed record TestStreamMessage17() : IStreamRequest<int>; public sealed record TestStreamMessage17Handler : IStreamRequestHandler<TestStreamMessage17, int> { public async IAsyncEnumerable<int> Handle(TestStreamMessage17 request, [EnumeratorCancellation] CancellationToken cancellationToken) { for (int i = 0; i < 3; i++) { await Task.Yield(); yield return i; } } }
// csharpier-ignore-end
