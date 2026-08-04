/*
* All or portions of this file Copyright (c) Amazon.com, Inc. or its affiliates or
* its licensors.
*
* For complete copyright and license terms please see the LICENSE at the root of this
* distribution (the "License"). All use of this software is governed by the License,
* or, if provided, by the license below or the license accompanying this file. Do not
* remove or modify any license notices. This file is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
*
*/

#if ENABLE_IL2CPP
namespace Aws.GameLift.Server
{
    using System;
    using log4net;
    using log4net.Core;

    /// <summary>
    /// ILog substitute for IL2CPP builds, where log4net cannot initialize because
    /// System.Configuration.ConfigurationManager is unsupported. Debug/Info are
    /// dropped; Warn/Error/Fatal are forwarded to UnityEngine.Debug.
    /// </summary>
    public sealed class UnityDebugLog : ILog
    {
        public bool IsDebugEnabled => false;

        public bool IsInfoEnabled => false;

        public bool IsWarnEnabled => true;

        public bool IsErrorEnabled => true;

        public bool IsFatalEnabled => true;

        public ILogger Logger => null;

        public void Debug(object message)
        {
        }

        public void Debug(object message, Exception exception)
        {
        }

        public void DebugFormat(string format, params object[] args)
        {
        }

        public void DebugFormat(string format, object arg0)
        {
        }

        public void DebugFormat(string format, object arg0, object arg1)
        {
        }

        public void DebugFormat(string format, object arg0, object arg1, object arg2)
        {
        }

        public void DebugFormat(IFormatProvider provider, string format, params object[] args)
        {
        }

        public void Info(object message)
        {
        }

        public void Info(object message, Exception exception)
        {
        }

        public void InfoFormat(string format, params object[] args)
        {
        }

        public void InfoFormat(string format, object arg0)
        {
        }

        public void InfoFormat(string format, object arg0, object arg1)
        {
        }

        public void InfoFormat(string format, object arg0, object arg1, object arg2)
        {
        }

        public void InfoFormat(IFormatProvider provider, string format, params object[] args)
        {
        }

        public void Warn(object message)
        {
            UnityEngine.Debug.LogWarning(message);
        }

        public void Warn(object message, Exception exception)
        {
            UnityEngine.Debug.LogWarning($"{message}\n{exception}");
        }

        public void WarnFormat(string format, params object[] args)
        {
            UnityEngine.Debug.LogWarningFormat(format, args);
        }

        public void WarnFormat(string format, object arg0)
        {
            UnityEngine.Debug.LogWarningFormat(format, arg0);
        }

        public void WarnFormat(string format, object arg0, object arg1)
        {
            UnityEngine.Debug.LogWarningFormat(format, arg0, arg1);
        }

        public void WarnFormat(string format, object arg0, object arg1, object arg2)
        {
            UnityEngine.Debug.LogWarningFormat(format, arg0, arg1, arg2);
        }

        public void WarnFormat(IFormatProvider provider, string format, params object[] args)
        {
            UnityEngine.Debug.LogWarningFormat(format, args);
        }

        public void Error(object message)
        {
            UnityEngine.Debug.LogError(message);
        }

        public void Error(object message, Exception exception)
        {
            UnityEngine.Debug.LogError($"{message}\n{exception}");
        }

        public void ErrorFormat(string format, params object[] args)
        {
            UnityEngine.Debug.LogErrorFormat(format, args);
        }

        public void ErrorFormat(string format, object arg0)
        {
            UnityEngine.Debug.LogErrorFormat(format, arg0);
        }

        public void ErrorFormat(string format, object arg0, object arg1)
        {
            UnityEngine.Debug.LogErrorFormat(format, arg0, arg1);
        }

        public void ErrorFormat(string format, object arg0, object arg1, object arg2)
        {
            UnityEngine.Debug.LogErrorFormat(format, arg0, arg1, arg2);
        }

        public void ErrorFormat(IFormatProvider provider, string format, params object[] args)
        {
            UnityEngine.Debug.LogErrorFormat(format, args);
        }

        public void Fatal(object message)
        {
            UnityEngine.Debug.LogError(message);
        }

        public void Fatal(object message, Exception exception)
        {
            UnityEngine.Debug.LogError($"{message}\n{exception}");
        }

        public void FatalFormat(string format, params object[] args)
        {
            UnityEngine.Debug.LogErrorFormat(format, args);
        }

        public void FatalFormat(string format, object arg0)
        {
            UnityEngine.Debug.LogErrorFormat(format, arg0);
        }

        public void FatalFormat(string format, object arg0, object arg1)
        {
            UnityEngine.Debug.LogErrorFormat(format, arg0, arg1);
        }

        public void FatalFormat(string format, object arg0, object arg1, object arg2)
        {
            UnityEngine.Debug.LogErrorFormat(format, arg0, arg1, arg2);
        }

        public void FatalFormat(IFormatProvider provider, string format, params object[] args)
        {
            UnityEngine.Debug.LogErrorFormat(format, args);
        }
    }
}
#endif
