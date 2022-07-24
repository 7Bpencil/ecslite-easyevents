## EasyEvents for [LeoEcsLite](https://github.com/Leopotam/ecslite)
* Boilerplate-free syntax for full life cycle of events - entities with single component.  
* No need to define filters, pools, and worlds in every place you want to use events, everything is inside one EventsBus object.  
* Special support for singleton events - no more silly situations, when you have to run foreach loop over filter, even if you sure, that there can be only one entity.
### Index
* [Usage examples](#usage-examples)
    * [Create events](#create-events)  
    * [Check events on existence](#check-events-on-existence)  
    * [Use or modify event bodies](#use-or-modify-event-bodies)  
    * [Destroy events](#destroy-events)  
    * [Destroy events of some types automatically](#destroy-events-of-some-types-automatically)  
* [Initialization](#initialization)  
    * [Event types](#event-component-aka-event-body-should-implement-one-and-only-one-event-type)
    * [EventsBus object](#create-eventsbus-object)
### Usage examples:
#### Create events:
```c#
public class TestCreatingEventsSystem : IEcsRunSystem
{
    public void Run(IEcsSystems systems)
    {
        var shared = systems.GetShared<SharedData>();
        var events = shared.EventsBus;

        // create new singleton event, if such event already exists, method returns body of existing one
        events.NewEventSingleton<PlayerReloadGunEvent>() = new PlayerReloadGunEvent {NextMag = ..., IsFastReload = ...};
        events.NewEventSingleton<PlayerMoveEvent>().Direction = ...; 
        events.NewEventSingleton<PlayerJumpEvent>();

        // create new usual/non-singleton/replicant event:
        events.NewEvent<CreateVFX>() = new CreateVFX {AssetPath = ..., Parent = ..., Position = ..., Orientation = ...};
        events.NewEvent<PlayActionMusic>().Type = ...
        events.NewEvent<TestEvent>();
    }
}
```
#### Check events on existence:
```c#
public class TestCheckingEventsSystem : IEcsRunSystem
{
    public void Run(IEcsSystems systems)
    {
        var shared = systems.GetShared<SharedData>();
        var events = shared.EventsBus;

        // check existence of singleton event
        if (events.HasEventSingleton<PlayerJumpEvent>()) {
            character.StartJumping()
        }

        // check existence of group of events
        if (!events.HasEvents<PlayActionMusic>()) {
            audio.PlayDefaultMusic();
        }
    }
}
```
#### Use or modify event bodies:
```c#
public class TestUsingModifyingEventsSystem : IEcsRunSystem
{
    public void Run(IEcsSystems systems)
    {
        var shared = systems.GetShared<SharedData>();
        var events = shared.EventsBus;
        
        // check existence of singleton event and get event body to use (method returns by value - C# limitation)
        if (events.HasEventSingleton<PlayerMoveEvent>(out var moveEventBody)) {
            character.Move(moveEventBody.Direction);
        }

        // get singleton event body by ref to modify its fields
        if (events.HasEventSingleton<PlayerMoveEvent>()) {
            ref var moveEventBody = ref events.GetEventBodySingleton<PlayerMoveEvent>();
            moveEventBody.Direction = -moveEventBody.Direction;
        }

        // get filter and pool of according event type
        foreach (var entity in events.GetEventBodies<CreateVFX>(out var creationEventsPool))
        {
            ref var eventBody = ref creationEventsPool.Get(entity);
            var effectAsset = GetAsset(eventBody.AssetPath);
            var effect = Object.Instantiate(effectAsset.Visual, eventBody.Position, eventBody.Orientation, eventBody.Parent);
            ...
        }
    }
}
```
#### Destroy events
```c#
public class TestEventsDestructionSystem : IEcsRunSystem
{
    public void Run(IEcsSystems systems)
    {
        var shared = systems.GetShared<SharedData>();
        var events = shared.EventsBus;
        
        // destroy singleton event, does nothing if event didn't exist in the first place
        events.DestroyEventSingleton<PlayerJumpEvent>();

        // destroy events you don't like
        foreach (var entity in events.GetEventBodies<CreateVFX>(out var vfxCreationEventsPool))
        {
            ref var eventBody = ref vfxCreationEventsPool.Get(entity);
            if (eventsBody.Position.y < 0) {
                vfxCreationEventsPool.Del(entity);
            }
        }
        
        // destroy all events of this type
        events.DestroyEvents<TestEvent>();
    }
}
```
#### Destroy events of some types automatically:
```c#
private void Start()
{
    world = new EcsWorld();
    sharedData = new SharedData
    {
        EventsBus = new EventsBus(),
        ...
    };

    systems = new EcsSystems(world, sharedData);
    systems
        // gameplay events
        ...
        // automatically remove all events of these types
        .Add(sharedData.EventsBus.GetDestroyEventsSystem()
            .IncSingleton<PlayerReloadGunEvent>()
            .IncSingleton<PlayerMoveEvent>()
            .IncSingleton<PlayerJumpEvent>()
            .IncReplicant<CreateVFX>()
            .IncReplicant<PlayActionMusic>()
            .IncReplicant<TestEvent>()
        )
        .Init();
}
```
### Initialization:
#### Event component (aka event body) should implement one (and only one) event type:
```c#
/// <summary>
/// Simultaneously there can be only one instance of this event type
/// </summary>
public interface IEventSingleton { }

/// <summary>
/// Simultaneously there can be multiple instances of this event type
/// </summary>
public interface IEventReplicant { }
```
```c#
public struct PlayerSwitchWeaponEvent : IEventSingleton { ... }
public struct CreateVFX : IEventReplicant { ... }
```
#### Create EventsBus object:
```c#
private void Start()
{
    world = new EcsWorld();
    sharedData = new SharedData
    {
        // EventsBus object will create its own internal EcsWorld to manage events
        EventsBus = new EventsBus(),
        ...
    };

    // you can get EventsBus' internal events world, but it's modification is not safe
#if UNITY_EDITOR
        UnityIntegration.EcsWorldObserver.Create (sharedData.EventsBus.GetEventsWorld());
#endif  

    // now you can use events in every system you like
    systems = new EcsSystems(world, sharedData);
    ...
}

// do not forget to destroy EventsBus and other worlds
private void OnDestroy()
{
    systems.Destroy();
    world.Destroy();
    sharedData.EventsBus.Destroy();
}
```
