using System;
using System.Reflection;
using MediatR;
using MediatR.Pipeline;
using SimpleInjector;

namespace ConsoleApp
{
    public static class ContainerExtensions
    {
        public static Container AddMediatr(this Container container, params Assembly[] handlerAssemblies)
        {
            container.RegisterSingleton<IMediator, Mediator>();
            container.Register(typeof(IRequestHandler<,>), handlerAssemblies);

            RegisterHandlers(container, typeof(INotificationHandler<>), handlerAssemblies);
            RegisterHandlers(container, typeof(IRequestExceptionAction<,>), handlerAssemblies);
            RegisterHandlers(container, typeof(IRequestExceptionHandler<,,>), handlerAssemblies);

            //Pipeline
            container.Collection.Register(typeof(IPipelineBehavior<,>), new Type[]
            {
                //typeof(RequestExceptionProcessorBehavior<,>),
                //typeof(RequestExceptionActionProcessorBehavior<,>),
                //typeof(RequestPreProcessorBehavior<,>),
                //typeof(RequestPostProcessorBehavior<,>)
            });
            container.Collection.Register(typeof(IRequestPreProcessor<>), new Type[] { });
            container.Collection.Register(typeof(IRequestPostProcessor<,>), new Type[] { });

            container.Register(() => new ServiceFactory(container.GetInstance), Lifestyle.Singleton);

            return container;
        }

        private static void RegisterHandlers(Container container, Type collectionType, Assembly[] assemblies)
        {
            // we have to do this because by default, generic type definitions (such as the Constrained Notification Handler) won't be registered
            var handlerTypes = container.GetTypesToRegister(collectionType, assemblies, new TypesToRegisterOptions
            {
                IncludeGenericTypeDefinitions = true,
                IncludeComposites = false
            });

            container.Collection.Register(collectionType, handlerTypes);
        }
    }
}