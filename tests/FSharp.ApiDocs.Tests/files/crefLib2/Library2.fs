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
/// <see cref="T:crefLib1.Class1" />
/// </summary>
type Class2() =
    /// <summary>
    /// <see cref="Unknown__Reference" />
    /// </summary>
    member this.Other = "more"

    /// <summary>
    /// This is a method in Class2 called Method0
    /// </summary>
    member this.Method0() = "more"

    /// <summary>
    /// This is a method in Class2 called Method1
    /// </summary>
    member this.Method1(_c: string) = "more"

    /// <summary>
    /// This is a method in Class2 called Method2
    /// </summary>
    member this.Method2(_c: string, _o: obj) = "more"


type GenericClass2<'T>() =
    /// This is a property in GenericClass2 called Property
    member this.Property = "more"

    /// This is a method in GenericClass2 called NonGenericMethod
    member this.NonGenericMethod(_c: 'T) = "more"

    /// This is a method in GenericClass2 called GenericMethod
    member this.GenericMethod(_c: 'T, _o: 'U) = "more"


type MathTest() =
    /// <summary>
    ///  This is XmlMath1 \(f(x)\)
    /// </summary>
    member this.XmlMath1 = "more"

    /// <summary>
    /// This is XmlMath2 \(\left\lceil \frac{\text{end} - \text{start}}{\text{step}} \right\rceil\)
    /// </summary>
    member this.XmlMath2 = "more"

    /// <summary>
    ///   <para>XmlMath3</para>
    ///   \[
    ///     \left\lceil \frac{\text{end} - \text{start}}{\text{step}} \right\rceil
    ///   \]
    /// </summary>
    member this.XmlMath3 = "more"

    /// <summary>
    ///   <para>XmlMath4</para>
    ///   \[
    ///     1 &lt; 2 &lt; 3 &gt; 0
    ///   \]
    /// </summary>
    member this.XmlMath4 = "more"

/// <summary>
/// Test
/// </summary>
type Class3() =
    /// <summary>
    /// <see cref="P:crefLib2.Class2.Other" />
    /// and <see cref="M:crefLib2.Class2.Method0" />
    /// and <see cref="M:crefLib2.Class2.Method1(System.String)" />
    /// and <see cref="M:crefLib2.Class2.Method2(System.String,System.Object)" />
    /// and <see cref="P:crefLib2.Class2.NotExistsProperty" />
    /// and <see cref="M:crefLib2.Class2.NotExistsMethod()" />
    /// and <see cref="T:crefLib2.GenericClass2`1" />
    /// and <see cref="P:crefLib2.GenericClass2`1.Property" />
    /// and <see cref="M:crefLib2.GenericClass2`1.NonGenericMethod(`0)" />
    /// and <see cref="M:crefLib2.GenericClass2`1.GenericMethod``1(`0,``0)" />
    /// and <see cref="P:crefLib2.GenericClass2`1.NotExistsProperty" />
    /// and <see cref="M:crefLib2.GenericClass2`1.NotExistsMethod()" />
    /// </summary>
    member this.X = "F#"

/// <summary>
/// Test
/// </summary>
type Class4() =
    /// <summary>
    /// <see cref="T:System.Reflection.Assembly" />
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
