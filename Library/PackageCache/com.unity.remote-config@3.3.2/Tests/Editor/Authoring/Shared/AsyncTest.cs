// WARNING: Auto generated code by Starbuck2. Modifications will be lost!

using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.Services.RemoteConfig.Tests.Editor.Authoring.Shared
{
    static class AsyncTest
    {
        public static IEnumerator AsCoroutine(Func<Task> test)
        {
            var task = test();
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.Exception != null)
            {
                if (task.Exception.InnerException != null)
                {
                    throw task.Exception.InnerException;
                }
                throw task.Exception;
            }
        }
    }
}
