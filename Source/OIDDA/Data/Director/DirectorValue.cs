using System;
using System.Collections.Generic;
using OIDDA.Data;
using FlaxEngine;

namespace OIDDA;

/// <summary>
/// Director Value
/// </summary>
public struct DirectorValue
{
    public DirectorCategory Category;
    public DirectorAction Action;
    public GameplayValue Value;
    public GameplayValue Min;
    public GameplayValue Max;
}
