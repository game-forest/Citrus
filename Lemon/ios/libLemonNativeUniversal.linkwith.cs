using System;
using ObjCRuntime;

[assembly: LinkWith ("libLemonNativeUniversal.a", LinkTarget.ArmV6 | LinkTarget.ArmV7 | LinkTarget.Simulator | LinkTarget.Arm64, ForceLoad = true)]
