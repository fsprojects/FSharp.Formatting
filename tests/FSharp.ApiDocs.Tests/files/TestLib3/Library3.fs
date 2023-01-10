namespace FsLib

module PartiallyDocumented =

    /// Should be omitted (but will generated a warning)
    /// [omit]
    let shouldBeOmitted() = ()

    /// <exclude/>
    /// <summary>
    /// Should be excluded
    /// </summary>
    let shouldBeExcluded1() = ()

    /// <summary>
    /// Should be excluded
    /// </summary>
    /// <exclude/>
    let shouldBeExcluded2() = ()

    // Should be excluded
    /// <exclude/>
    let shouldBeExcluded3() = ()

    /// <exclude/>
    // Should be excluded
    let shouldBeExcluded4() = ()

    /// <exclude/>
    let shouldBeExcluded5() = ()

    /// <exclude/>
    /// This triple-'/' comment auto-creates a summary element with the exclude tag escaped into its text
    let shouldBeExcluded6() = ()

    /// This triple-'/' comment auto-creates a summary element with the exclude tag escaped into its text
    /// <exclude/>
    let shouldBeExcluded7() = ()

    /// <summary>
    /// Returns unit
    /// </summary>
    let returnUnit() = ()

    /// <summary>
    /// Should be excluded
    /// </summary>
    /// <exclude/>
    module NotDocumented1 =
        let a = 10

    /// <exclude/>
    module NotDocumented2 =
        let a = 10

    /// This triple-'/' comment auto-creates a summary element with the exclude tag escaped into its text
    /// <exclude/>
    module NotDocumented3 =
        let a = 10

    let x = 10

