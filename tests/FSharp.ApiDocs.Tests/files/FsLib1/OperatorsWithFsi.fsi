module FsLib.OperatorsWithFsi

/// <summary>The binary operator 1</summary>
val (<&>): 'T -> 'T -> bool when 'T: comparison

/// <summary>The binary operator 2</summary>
val (?<?): x: 'T -> y: 'T -> bool when 'T: comparison

/// <summary>The unary operator 1</summary>
val (<?): 'T -> 'T

/// <summary>The unary operator 2</summary>
val (<?>): x: 'T -> 'T
