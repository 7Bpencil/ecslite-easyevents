using System;
using System.Collections.Generic;
using Leopotam.EcsLite;
 for  otto events  of eveaccording event
namespace SevenBoldPencil.EasyEvents
{
    /// <summary>
    /// Simultaneously there can be only one instance of this event type
    /// </summary>
    public interface IEventSingleton { }

    /// <summary>
    /// Simultaneously there can be multiple instances of this event type
    /// </summary>
    public interface IEventReplicant { }

    public class EventsSupport
    {
        private readonly EcsWorld eventWorld;
        private readonly Dictionary<Type, int> singletonEntities;
        private readonly Dictionary<Type, EcsFilter> cachedFilters;

        public EventsSupport(EcsWorld eventWorld, int capacityEvents = 8, int capacityEventsSingleton = 8)
        {
            this.eventWorld = eventWorld;
            singletonEntities = new Dictionary<Type, int>(capacityEventsSingleton);
            cachedFilters = new Dictionary<Type, EcsFilter>(capacityEvents);
        }

        #region EventsSingleton

        public ref T NewEventSingleton<T>() where T : struct, IEventSingleton
        {
            var type = typeof(T);
            var eventPool = eventWorld.GetPool<T>();
            if (!singletonEntities.TryGetValue(type, out var eventEntity)) {
                eventEntity = eventWorld.NewEntity();
                singletonEntities.Add(type, eventEntity);
                return ref eventPool.Add(eventEntity);
            }

            return ref eventPool.Get(eventEntity);
        }

        public bool HasEventSingleton<T>() where T : struct, IEventSingleton
        {
            return singletonEntities.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Returns by value, use GetEventBodySingleton to get event body by ref
        /// </summary>
        /// <param name="eventBody"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool HasEventSingleton<T>(out T eventBody) where T : struct, IEventSingleton
        {
            var hasEvent = singletonEntities.TryGetValue(typeof(T), out var eventEntity);
            eventBody = hasEvent ? eventWorld.GetPool<T>().Get(eventEntity) : default;
            return hasEvent;
        }

        /// <summary>
        /// Throws exception if event doesn't exist
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public ref T GetEventBodySingleton<T>() where T : struct, IEventSingleton
        {
            var eventEntity = singletonEntities[typeof(T)];
            var eventPool = eventWorld.GetPool<T>();
            return ref eventPool.Get(eventEntity);
        }

        public void DestroyEventSingleton<T>() where T : struct, IEventSingleton
        {
            var type = typeof(T);
            if (singletonEntities.TryGetValue(type, out var eventEntity)) {
                eventWorld.DelEntity(eventEntity);
                singletonEntities.Remove(type);
            }
        }

        #endregion

        #region Events

        public ref T NewEvent<T>() where T : struct, IEventReplicant
        {
            var newEntity = eventWorld.NewEntity();
            return ref eventWorld.GetPool<T>().Add(newEntity);
        }

        private EcsFilter GetFilter<T>() where T : struct, IEventReplicant
        {
            var type = typeof(T);
            if (!cachedFilters.TryGetValue(type, out var filter)) {
                filter = eventWorld.Filter<T>().End();
                cachedFilters.Add(type, filter);
            }

            return filter;
        }

        public EcsFilter GetEventBodies<T>(out EcsPool<T> pool) where T : struct, IEventReplicant
        {
            pool = eventWorld.GetPool<T>();
            return GetFilter<T>();
        }

        public bool HasEvents<T>() where T : struct, IEventReplicant
        {
            var filter = GetFilter<T>();
            return filter.GetEntitiesCount() != 0;
        }

        /// <summary>
        /// Destroys all events of this type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void DestroyEvents<T>() where T : struct, IEventReplicant
        {
            foreach (var eventEntity in GetFilter<T>()) {
                eventWorld.DelEntity(eventEntity);
            }
        }

        #endregion

        #region DestroyEventsSystem

        public DestroyEventsSystem GetDestroyEventsSystem(int capacity = 16)
        {
            return new DestroyEventsSystem(this, capacity);
        }

        public class DestroyEventsSystem : IEcsRunSystem
        {
            private readonly EventsSupport eventsSupport;
            private readonly List<Action> destructionActions;

            public DestroyEventsSystem(EventsSupport eventsSupport, int capacity)
            {
                this.eventsSupport = eventsSupport;
                destructionActions = new List<Action>(capacity);
            }

            public void Run(EcsSystems systems)
            {
                foreach (var action in destructionActions) {
                    action();
                }
            }

            public DestroyEventsSystem IncReplicant<R>() where R : struct, IEventReplicant {
                destructionActions.Add(() => eventsSupport.DestroyEvents<R>());
                return this;
            }

            public DestroyEventsSystem IncSingleton<S>() where S : struct, IEventSingleton {
                destructionActions.Add(() => eventsSupport.DestroyEventSingleton<S>());
                return this;
            }
        }

        #endregion
    }
}
