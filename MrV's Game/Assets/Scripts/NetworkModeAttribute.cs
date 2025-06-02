using System;
using UnityEngine;

public enum NetworkMode
{
    LAN,
    PHOTON
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class NetworkModeAttribute : Attribute
{
    public NetworkMode Mode { get; }

    public NetworkModeAttribute(NetworkMode mode)
    {
        Mode = mode;
    }
}