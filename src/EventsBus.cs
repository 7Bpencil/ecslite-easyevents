using System;
using System.Collections.Generic;
using Leopotam.EcsLite;

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

    public class EventsBus
    {
        private readonly EcsWorld eventsWorld;
        private readonly Dictionary<Type, int> singletonEntities;
        private readonly Dictionary<Type, EcsFilter> cachedFilters;

        public EventsBus(int capacityEvents = 8, int capacityEventsSingleton = 8)
        {
            eventsWorld = new EcsWorld();
            singletonEntities = new Dictionary<Type, int>(capacityEventsSingleton);
            cachedFilters = new Dictionary<Type, EcsFilter>(capacityEvents);
        }

        #region EventsSingleton

        public ref T NewEventSingleton<T>() where T : struct, IEventSingleton
        {
            var type = typeof(T);
            var eventsPool = eventsWorld.GetPool<T>();
            if (!singletonEntities.TryGetValue(type, out var eventEntity)) {
                eventEntity = eventsWorld.NewEntity();
                singletonEntities.Add(type, eventEntity);
                return ref eventsPool.Add(eventEntity);
            }

            return ref eventsPool.Get(eventEntity);
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
            eventBody = hasEvent ? eventsWorld.GetPool<T>().Get(eventEntity) : default;
            return hasEvent;
        }

        public ref T GetEventBodySingleton<T>() where T : struct, IEventSingleton
        {
            var eventEntity = singletonEntities[typeof(T)];
            var eventsPool = eventsWorld.GetPool<T>();
            return ref eventsPool.Get(eventEntity);
        }

        public void DestroyEventSingleton<T>() where T : struct, IEventSingleton
        {
            var type = typeof(T);
            if (singletonEntities.TryGetValue(type, out var eventEntity)) {
                eventsWorld.DelEntity(eventEntity);
                singletonEntities.Remove(type);
            }
        }

        #endregion

        #region Events

        public ref T NewEvent<T>() where T : struct, IEventReplicant
        {
            var newEntity = eventsWorld.NewEntity();
            return ref eventsWorld.GetPool<T>().Add(newEntity);
        }

        private EcsFilter GetFilter<T>() where T : struct, IEventReplicant
        {
            var type = typeof(T);
            if (!cachedFilters.TryGetValue(type, out var filter)) {
                filter = eventsWorld.Filter<T>().End();
                cachedFilters.Add(type, filter);
            }

            return filter;
        }

        public EcsFilter GetEventBodies<T>(out EcsPool<T> pool) where T : struct, IEventReplicant
        {
            pool = eventsWorld.GetPool<T>();
            return GetFilter<T>();
        }

        public bool HasEvents<T>() where T : struct, IEventReplicant
        {
            var filter = GetFilter<T>();
            return filter.GetEntitiesCount() != 0;
        }

        public void DestroyEvents<T>() where T : struct, IEventReplicant
        {
            foreach (var eventEntity in GetFilter<T>()) {
                eventsWorld.DelEntity(eventEntity);
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
            private readonly EventsBus eventsBus;
            private readonly List<Action> destructionActions;

            public DestroyEventsSystem(EventsBus eventsBus, int capacity)
            {
                this.eventsBus = eventsBus;
                destructionActions = new List<Action>(capacity);
            }

            public void Run(IEcsSystems systems)
            {
                foreach (var action in destructionActions) {
                    action();
                }
            }

            public DestroyEventsSystem IncReplicant<R>() where R : struct, IEventReplicant {
                destructionActions.Add(() => eventsBus.DestroyEvents<R>());
                return this;
            }

            public DestroyEventsSystem IncSingleton<S>() where S : struct, IEventSingleton {
                destructionActions.Add(() => eventsBus.DestroyEventSingleton<S>());
                return this;
            }
        }

        #endregion

        /// <summary>
        /// External modification of events world can lead to Unforeseen Consequences
        /// </summary>
        /// <returns></returns>
        public EcsWorld GetEventsWorld()
        {
            return eventsWorld;
        }

        public void Destroy()
        {
            singletonEntities.Clear();
            cachedFilters.Clear();
            eventsWorld.Destroy();
        }
    }
}
