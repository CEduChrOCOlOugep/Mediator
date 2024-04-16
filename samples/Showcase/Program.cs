using System;
using System.Diagnostics;
using System.Linq;
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

await mediator.Send(new TestMessage1());
await ((Mediator.Mediator)mediator).Send(new TestMessage1());
await mediator.Send((object)new TestMessage1());

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
public sealed record TestMessage1() : IRequest; public sealed record TestMessage1Handler : IRequestHandler<TestMessage1> { public ValueTask<Unit> Handle(TestMessage1 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage2() : IRequest; public sealed record TestMessage2Handler : IRequestHandler<TestMessage2> { public ValueTask<Unit> Handle(TestMessage2 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage3() : IRequest; public sealed record TestMessage3Handler : IRequestHandler<TestMessage3> { public ValueTask<Unit> Handle(TestMessage3 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage4() : IRequest; public sealed record TestMessage4Handler : IRequestHandler<TestMessage4> { public ValueTask<Unit> Handle(TestMessage4 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage5() : IRequest; public sealed record TestMessage5Handler : IRequestHandler<TestMessage5> { public ValueTask<Unit> Handle(TestMessage5 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage6() : IRequest; public sealed record TestMessage6Handler : IRequestHandler<TestMessage6> { public ValueTask<Unit> Handle(TestMessage6 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage7() : IRequest; public sealed record TestMessage7Handler : IRequestHandler<TestMessage7> { public ValueTask<Unit> Handle(TestMessage7 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage8() : IRequest; public sealed record TestMessage8Handler : IRequestHandler<TestMessage8> { public ValueTask<Unit> Handle(TestMessage8 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage9() : IRequest; public sealed record TestMessage9Handler : IRequestHandler<TestMessage9> { public ValueTask<Unit> Handle(TestMessage9 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage10() : IRequest; public sealed record TestMessage10Handler : IRequestHandler<TestMessage10> { public ValueTask<Unit> Handle(TestMessage10 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage11() : IRequest; public sealed record TestMessage11Handler : IRequestHandler<TestMessage11> { public ValueTask<Unit> Handle(TestMessage11 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage12() : IRequest; public sealed record TestMessage12Handler : IRequestHandler<TestMessage12> { public ValueTask<Unit> Handle(TestMessage12 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage13() : IRequest; public sealed record TestMessage13Handler : IRequestHandler<TestMessage13> { public ValueTask<Unit> Handle(TestMessage13 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage14() : IRequest; public sealed record TestMessage14Handler : IRequestHandler<TestMessage14> { public ValueTask<Unit> Handle(TestMessage14 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage15() : IRequest; public sealed record TestMessage15Handler : IRequestHandler<TestMessage15> { public ValueTask<Unit> Handle(TestMessage15 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage16() : IRequest; public sealed record TestMessage16Handler : IRequestHandler<TestMessage16> { public ValueTask<Unit> Handle(TestMessage16 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage17() : IRequest; public sealed record TestMessage17Handler : IRequestHandler<TestMessage17> { public ValueTask<Unit> Handle(TestMessage17 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage18() : IRequest; public sealed record TestMessage18Handler : IRequestHandler<TestMessage18> { public ValueTask<Unit> Handle(TestMessage18 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage19() : IRequest; public sealed record TestMessage19Handler : IRequestHandler<TestMessage19> { public ValueTask<Unit> Handle(TestMessage19 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage20() : IRequest; public sealed record TestMessage20Handler : IRequestHandler<TestMessage20> { public ValueTask<Unit> Handle(TestMessage20 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage21() : IRequest; public sealed record TestMessage21Handler : IRequestHandler<TestMessage21> { public ValueTask<Unit> Handle(TestMessage21 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage22() : IRequest; public sealed record TestMessage22Handler : IRequestHandler<TestMessage22> { public ValueTask<Unit> Handle(TestMessage22 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage23() : IRequest; public sealed record TestMessage23Handler : IRequestHandler<TestMessage23> { public ValueTask<Unit> Handle(TestMessage23 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage24() : IRequest; public sealed record TestMessage24Handler : IRequestHandler<TestMessage24> { public ValueTask<Unit> Handle(TestMessage24 request, CancellationToken cancellationToken) => default; }
public sealed record TestMessage25() : IRequest; public sealed record TestMessage25Handler : IRequestHandler<TestMessage25> { public ValueTask<Unit> Handle(TestMessage25 request, CancellationToken cancellationToken) => default; }
// csharpier-ignore-end
