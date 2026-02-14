# Class Diagram - Decoupled Worker Manager Architecture

```mermaid
classDiagram
    class Message {
        <<record>>
        +string message
    }

    class ISubscriber {
        <<interface>>
        +string Id
        +Consume(Message) void
    }

    class Subscriber {
        +string Id
        +Subscriber(string id)
        +Consume(Message) void
    }

    class SleepySubscriber {
        +string Id
        -int _sleepTimeMs
        +SleepySubscriber(string id, int sleepTimeMs)
        +Consume(Message) void
    }

    class SubscriberOffSet {
        +int OffSet
        +ISubscriber Subscriber
        +SubscriberOffSet(int offSet, ISubscriber subscriber)
    }

    class SubscriberWorker {
        -List~Message~ _messageLog
        -SubscriberOffSet _subscriberOffset
        -Thread _workerThread
        -bool _isRunning
        +SubscriberWorker(List~Message~ messageLog, SubscriberOffSet subscriberOffSet)
        +Start() void
        -Run() void
        +WakeUp() void
        +Stop() void
    }

    class SubscriberWorkerManager {
        -Dictionary~string, SubscriberWorker~ _workers
        -Dictionary~string, SubscriberOffSet~ _subscriberOffsets
        -List~Message~ _messageLog
        +SubscriberWorkerManager(List~Message~ messageLog)
        +RegisterSubscriber(ISubscriber) void
        +NotifyNewMessage() void
        +ResetOffset(string subscriberId, int newOffset) void
        +ShowOffsets() void
        +StopAll() void
    }

    class Topic {
        -List~Message~ _messageLog
        -SubscriberWorkerManager _workerManager
        +string Name
        +Topic(string name)
        +Subscribe(ISubscriber) void
        +Publish(Message) void
        +ResetOffset(ISubscriber, int) void
        +ShowOffsets() void
        +Shutdown() void
    }

    class MessageBroker {
        -Dictionary~string, Topic~ _topics
        +MessageBroker()
        +CreateTopic(string) Topic
        +SendMessage(Topic, Message) void
    }

    class Program {
        +Main(string[] args) void
    }

    %% Inheritance relationships
    ISubscriber <|.. Subscriber : implements
    ISubscriber <|.. SleepySubscriber : implements

    %% Composition relationships
    SubscriberOffSet o-- ISubscriber : contains
    SubscriberWorker *-- SubscriberOffSet : owns
    SubscriberWorker o-- Message : processes
    
    SubscriberWorkerManager *-- SubscriberWorker : manages
    SubscriberWorkerManager *-- SubscriberOffSet : tracks
    SubscriberWorkerManager o-- Message : accesses
    
    Topic *-- SubscriberWorkerManager : owns
    Topic *-- Message : stores
    Topic ..> ISubscriber : uses
    
    MessageBroker *-- Topic : manages
    MessageBroker ..> Message : uses
    
    Program ..> MessageBroker : uses
    Program ..> Topic : uses
    Program ..> ISubscriber : creates
    Program ..> Message : creates

    %% Notes
    note for SubscriberWorkerManager "Manages worker thread lifecycle\nDecouples threading from Topic\nCentralized worker coordination"
    note for Topic "Focuses on message storage\nDelegates worker management\nSimplified responsibilities"
    note for SubscriberWorker "Runs on dedicated thread\nProcesses messages sequentially\nSupports offset replay"
```

## Architecture Overview

### Core Components

**Message Layer**
- `Message`: Immutable record representing a message
- `ISubscriber`: Interface for message consumers
- `Subscriber` / `SleepySubscriber`: Concrete implementations

**Offset Management**
- `SubscriberOffSet`: Tracks subscriber position in message log

**Worker Layer** (Decoupled)
- `SubscriberWorker`: Individual worker thread for one subscriber
- `SubscriberWorkerManager`: Centralized lifecycle management for all workers

**Messaging Layer**
- `Topic`: Message storage and subscription coordination
- `MessageBroker`: Multi-topic management

### Key Design Principles

1. **Separation of Concerns**
   - Topic: Message storage and publishing
   - SubscriberWorkerManager: Thread lifecycle management
   - SubscriberWorker: Message consumption logic

2. **Single Responsibility**
   - Each class has one clear purpose
   - Worker management is isolated from messaging logic

3. **Dependency Direction**
   - Topic depends on SubscriberWorkerManager
   - Manager depends on Worker
   - Worker depends on SubscriberOffSet and Message log

4. **Encapsulation**
   - Worker threads are hidden behind Manager interface
   - Topic doesn't know about threading details
   - Clean shutdown through Manager

### Benefits

- **Testability**: Can test Topic without threads
- **Flexibility**: Easy to swap threading strategy
- **Maintainability**: Clear boundaries between components
- **Resource Management**: Centralized worker lifecycle control
