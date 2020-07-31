namespace crefLib2

/// <summary>
/// <see cref="Class2" />
/// </summary>
type Class1() = 
    /// <summary>
    /// None
    /// </summary>
    member this.X = "F#"

/// <summary>
/// <see cref="crefLib1.Class1" />
/// </summary>
type Class2() = 
    /// <summary>
    /// <see cref="Unknown__Reference" />
    /// </summary>
    member this.Other = "more"

    
/// <summary>
/// Test
/// </summary>
type Class3() = 
    /// <summary>
    /// <see cref="P:crefLib2.Class2.Other" />
    /// </summary>
    member this.X = "F#"

/// <summary>
/// Test
/// </summary>
type Class4() = 
    /// <summary>
    /// <see cref="System.Reflection.Assembly" />
    /// </summary>
    member this.X = "F#"


    
/// <summary>
/// <see cref="T:crefLib2.Class2" />
/// </summary>
type Class5() = 
    /// <summary>
    /// None
    /// </summary>
    member this.X = "F#"

/// <summary>
/// <see cref="T:crefLib1.Class1" />
/// </summary>
type Class6() = 
    /// <summary>
    /// <see cref="!:Unknown__Reference" />
    /// </summary>
    member this.Other = "more"

    
/// <summary>
/// Test
/// </summary>
type Class7() = 
    /// <summary>
    /// <see cref="P:crefLib2.Class2.Other" />
    /// </summary>
    member this.X = "F#"

/// <summary>
/// Test
/// </summary>
type Class8() = 
    /// <summary>
    /// <see cref="T:System.Reflection.Assembly" />
    /// </summary>
    member this.X = "F#"