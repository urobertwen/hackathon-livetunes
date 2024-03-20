// WARNING: Auto generated code. Modifications will be lost!
// Original source 'com.unity.services.shared' @0.0.6.
using System;

namespace Unity.Services.RemoteConfig.Authoring.Editor.Shared.DependencyInversion
{
    class DependencyNotFoundException : Exception
    {
        public DependencyNotFoundException(Type serviceType)
            : base($"Could not find factory for {serviceType.Name}. Make sure that {serviceType.Name} was registered to your ServiceCollection")
        {
        }
    }
}
