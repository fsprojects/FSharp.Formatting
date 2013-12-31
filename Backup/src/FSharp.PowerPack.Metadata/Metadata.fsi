// (c) Microsoft Corporation 2005-2009
namespace Microsoft.FSharp.Metadata

open System.Collections.ObjectModel

type [<Sealed>] SourceLocation = 
   member Document : string
   member StartLine : int
   member StartColumn : int
   member EndLine : int
   member EndColumn : int
     
type [<Sealed>] FSharpAssembly = 

    /// Get the object representing the F# core library (FSharp.Core.dll) for the running program
    static member FSharpLibrary : FSharpAssembly

    /// This is one way of starting the loading process off. Dependencies are automatically
    /// resolved by calling System.Reflection.Assembly.Load.
    static member FromAssembly : System.Reflection.Assembly -> FSharpAssembly

    /// This is one way of starting the loading process off. 
    static member FromFile : fileName: string (* * loader:System.Func<string,FSharpAssembly> *) -> FSharpAssembly

    /// Holds the full qualified assembly name
    member QualifiedName: string; 
    
    /// Get the System.Reflection.Assembly object for the assembly
    member ReflectionAssembly: System.Reflection.Assembly
      
    /// Return the System.Reflection.Assembly object for the assembly
    member GetEntity : string -> FSharpEntity
      
      /// A hint as to where does the code for the CCU live (e.g what was the tcConfig.implicitIncludeDir at compilation time for this DLL?) 
    member CodeLocation: string; 
      
      /// A handle to the full specification of the contents of the module contained in this Assembly 
    member Entities:  ReadOnlyCollection<FSharpEntity>

/// Represents an F# type or module
and [<Sealed>] FSharpEntity = 

      /// Return the FSharpEntity corresponding to a .NET type
    static member FromType : System.Type -> FSharpEntity

      /// Get the name of the type or module, possibly with `n mangling  
    member LogicalName: string;

      /// Get the compiled name of the type or module, possibly with `n mangling. This is identical to LogicalName
      /// unless the CompiledName attribute is used.
    member CompiledName: string;

      /// Get the name of the type or module as displayed in F# code
    member DisplayName: string;

      /// Get the namespace containing the type or module, if any
    member Namespace: string;

      /// Get the entity containing the type or module, if any
    member DeclaringEntity: FSharpEntity;

      /// Get the fully qualified name of the type or module
    member QualifiedName: string; 


      /// Get the declaration location for the type constructor 
    member DeclarationLocation: SourceLocation; 

      /// Indicates the entity is a measure, type or exception abbreviation
    member IsAbbreviation   : bool

      /// Indicates the entity is record type
    member IsRecord   : bool

      /// Indicates the entity is union type
    member IsUnion   : bool

      /// Indicates the entity is a struct or enum
    member IsValueType : bool


      /// Indicates the entity is an F# module definition
    member IsModule: bool; 

      /// Get the generic parameters, possibly including unit-of-measure parameters
    member GenericParameters: ReadOnlyCollection<FSharpGenericParameter>

      /// Indicates that a module is compiled to a class with the given mangled name. The mangling is reversed during lookup 
    member HasFSharpModuleSuffix : bool

      /// Indicates the entity is a measure definition
    member IsMeasure: bool;

      /// Indicates an F# exception declaration
    member IsExceptionDeclaration: bool; 

    /// If true, then this is a reference to something in some .NET assembly from another .NET language
    member IsExternal : bool

    /// Get the System.Type for the type
    ///
    /// Raises InvalidOperationException if the type is an abbreviation or has an assembly code representation.
    member ReflectionType : System.Type  

    /// Get the System.Reflection.Assembly for the type
    ///
    /// May raise an exception if an assembly load fails
    member ReflectionAssembly : System.Reflection.Assembly


      /// Get the XML documentation signature for the entity
    member XmlDocSig: string;

      /// Indicates the type is implemented through a mapping to IL assembly code. THis is only
      /// true for types in FSharp.Core.dll
    member HasAssemblyCodeRepresentation: bool 

    
      /// Indicates the type prefers the "tycon<a,b>" syntax for display etc. 
    member UsesPrefixDisplay: bool;                   

      /// Get the declared attributes for the type 
    member Attributes: ReadOnlyCollection<FSharpAttribute>;     

      /// Interface implementations - boolean indicates compiler-generated 
    member Implements : ReadOnlyCollection<FSharpType>;  

      /// Base type, if any 
    member BaseType : FSharpType;


      /// Properties, methods etc. with implementations, also values in a module
    member MembersOrValues : ReadOnlyCollection<FSharpMemberOrVal>;

    member NestedEntities : ReadOnlyCollection<FSharpEntity>

      /// Get the fields of the class, struct or enum 
    member RecordFields : ReadOnlyCollection<FSharpRecordField>

    member AbbreviatedType   : FSharpType 

      /// Get the cases of a discriminated union
    member UnionCases : ReadOnlyCollection<FSharpUnionCase>


#if TODO
      /// Indicates the type is implemented as IL assembly code using a closed type in ILDASM syntax
      // NOTE: consider returning a System.Type
    member GetAssemblyCodeRepresentation : unit -> string 


    //   /// Indicates the type is a delegate with the given Invoke signature 
    // member TyconDelegateSlotSig : SlotSig option


#endif
      /// Get the declared accessibility of the type
    member Accessibility: FSharpAccessibility; 

      /// Get the declared accessibility of the representation, not taking signatures into account 
    member RepresentationAccessibility: FSharpAccessibility;

    static member op_Equality : FSharpEntity * FSharpEntity -> bool
    static member op_Inequality : FSharpEntity * FSharpEntity -> bool

and [<Sealed>] FSharpUnionCase =
      /// Get the name of the case 
    member Name: string; 
      /// Get the range of the name of the case 
    member DeclarationLocation : SourceLocation
    /// Get the data carried by the case. 
    member Fields: ReadOnlyCollection<FSharpRecordField>;
      /// Get type constructed by the case. Normally exactly the type of the enclosing type, sometimes an abbreviation of it 
    member ReturnType: FSharpType;
      /// Gete the name of the case in generated IL code 
    member CompiledName: string;
      /// Get the XML documentation signature for the case 
    member XmlDocSig: string;

      ///  Indicates the declared visibility of the union constructor, not taking signatures into account 
    member Accessibility: FSharpAccessibility; 

      /// Get the attributes for the case, attached to the generated static method to make instances of the case 
    member Attributes: ReadOnlyCollection<FSharpAttribute>;

    static member op_Equality : FSharpUnionCase * FSharpUnionCase -> bool
    static member op_Inequality : FSharpUnionCase * FSharpUnionCase -> bool


and [<Sealed>] FSharpRecordField =
    /// Is the field declared in F#? 
    member IsMutable: bool;
      /// Get the XML documentation signature for the field 
    member XmlDocSig: string;
      /// Get the type of the field, w.r.t. the generic parameters of the enclosing type constructor 
    member Type: FSharpType;
      /// Indicates a static field 
    member IsStatic: bool;
      /// Indicates a compiler generated field, not visible to Intellisense or name resolution 
    member IsCompilerGenerated: bool;
      /// Get the declaration location of the field 
    member DeclarationLocation: SourceLocation;
      /// Get the attributes attached to generated property 
    member PropertyAttributes: ReadOnlyCollection<FSharpAttribute>; 
      /// Get the attributes attached to generated field 
    member FieldAttributes: ReadOnlyCollection<FSharpAttribute>; 
      /// Get the name of the field 
    member Name : string

#if TODO
      /// Get the default initialization info, for static literals 
    member LiteralValue: obj; 
#endif
      ///  Indicates the declared visibility of the field, not taking signatures into account 
    member Accessibility: FSharpAccessibility; 

    static member op_Equality : FSharpRecordField * FSharpRecordField -> bool
    static member op_Inequality : FSharpRecordField * FSharpRecordField -> bool

and [<Sealed>] FSharpAccessibility = 
#if TODO
    member IsPublic : bool
    member IsPrivate : bool
    member IsInternal : bool
#endif

    class
    end
        
and [<Sealed>] FSharpGenericParameter = 
    /// Get the name of the generic parameter 
    member Name: string
    /// Get the range of the generic parameter 
    member DeclarationLocation : SourceLocation; 
       
    /// Indicates if this is a measure variable
    member IsMeasure : bool

    /// Get the documentation for the type parameter. 
    member XmlDoc : ReadOnlyCollection<string>;
       
    /// Indicates if this is a statically resolved type variable
    member IsSolveAtCompileTime : bool 

    /// Get the declared attributes of the type parameter. 
    member Attributes: ReadOnlyCollection<FSharpAttribute>;                      
       
    /// Get the declared or inferred constraints for the type parameter
    member Constraints: ReadOnlyCollection<FSharpGenericParameterConstraint>; 

    static member op_Equality : FSharpGenericParameter * FSharpGenericParameter -> bool
    static member op_Inequality : FSharpGenericParameter * FSharpGenericParameter -> bool


and [<Sealed>][<NoEquality>][<NoComparison>] 
    FSharpGenericParameterConstraint = 
    /// Indicates a constraint that a type is a subtype of the given type 
    member IsCoercesToConstraint : bool
    member CoercesToTarget : FSharpType 

    /// Indicates a default value for an inference type variable should it be netiher generalized nor solved 
    member IsDefaultsToConstraint : bool
    member DefaultsToPriority : int
    member DefaultsToTarget : FSharpType

    /// Indicates a constraint that a type has a 'null' value 
    member IsSupportsNullConstraint  : bool

    /// Indicates a constraint that a type supports F# generic comparison
    member IsComparisonConstraint  : bool

    /// Indicates a constraint that a type supports F# generic equality
    member IsEqualityConstraint  : bool

    /// Indicates a constraint that a type is an unmanaged type
    member IsUnmanagedConstraint  : bool

    /// Indicates a constraint that a type has a member with the given signature 
    member IsMemberConstraint : bool
    member MemberSources : ReadOnlyCollection<FSharpType>
    member MemberName : string 
    member MemberIsStatc : bool
    member MemberArgumentTypes : ReadOnlyCollection<FSharpType>
    member MemberReturnType : FSharpType 

    /// Indicates a constraint that a type is a non-Nullable value type 
    member IsNonNullableValueTypeConstraint : bool
    
    /// Indicates a constraint that a type is a reference type 
    member IsReferenceTypeConstraint  : bool

    /// Indicates a constraint that a type is a simple choice between one of the given ground types. Used by printf format strings.
    member IsSimpleChoiceConstraint : bool
    member SimpleChoices : ReadOnlyCollection<FSharpType>

    /// Indicates a constraint that a type has a parameterless constructor 
    member IsRequiresDefaultConstructorConstraint  : bool

    /// Indicates a constraint that a type is an enum with the given underlying 
    member IsEnumConstraint : bool
    member EnumConstraintTarget : FSharpType 
    
    /// Indicates a constraint that a type is a delegate from the given tuple of args to the given return type 
    member IsDelegateConstraint : bool
    member DelegateTupledArgumentType : FSharpType
    member DelegateReturnType : FSharpType 


and FSharpInlineAnnotation = 
   | PsuedoValue = 3
   /// Indictes the value is inlined but the code for the function still exists, e.g. to satisfy interfaces on objects, but that it is also always inlined 
   | AlwaysInline = 2
   | OptionalInline = 1
   | NeverInline = 0

and [<Sealed>] FSharpMemberOrVal = 
    member EnclosingEntity : FSharpEntity
    
    /// Get the declaration location of the member or value
    member DeclarationLocation: SourceLocation
    
    /// Get the typars of the member or value
    member GenericParameters: ReadOnlyCollection<FSharpGenericParameter>

    /// Get the full type of the member or value when used as a first class value
    member Type: FSharpType

    /// Indicates if this is a compiler generated value
    member IsCompilerGenerated : bool

    /// Get a result indicating if this is a must-inline value
    member InlineAnnotation : FSharpInlineAnnotation

    /// Indicates if this is a mutable value
    member IsMutable : bool

    /// Get the reflection object for this member
    
    [<System.Obsolete("This member does not yet return correct results for overloaded members")>]
    member ReflectionMemberInfo :System.Reflection.MemberInfo

    /// Indicates if this is a module or member value
    member IsModuleValueOrMember : bool

    /// Indicates if this is an extension member?
    member IsExtensionMember : bool

    /// Indicates if this is a member, including extension members?
    member IsMember : bool

    /// Indicates if this is an abstract member?
    member IsDispatchSlot : bool

    /// Indicates if this is a getter method for a property
    member IsGetterMethod: bool 

    /// Indicates if this is a setter method for a property
    member IsSetterMethod: bool 

    /// Indicates if this is an instance member, when seen from F#?
    member IsInstanceMember : bool 
    
    /// Indicates if this is an implicit constructor?
    member IsImplicitConstructor : bool
    
    /// Indicates if this is an F# type function
    member IsTypeFunction : bool

    /// Indicates if this value or member is an F# active pattern
    member IsActivePattern : bool
      
      /// Get the member name in compiled code
    member CompiledName: string

      /// Get the logical name of the member
    member LogicalName: string

      /// Get the logical enclosing entity, which for an extension member is type being extended
    member LogicalEnclosingEntity: FSharpEntity

      /// Get the name as presented in F# error messages and documentation
    member DisplayName : string

    member CurriedParameterGroups : ReadOnlyCollection<ReadOnlyCollection<FSharpParameter>>

    member ReturnParameter : FSharpParameter

      /// Custom attributes attached to the value. These contain references to other values (i.e. constructors in types). Mutable to fixup  
      /// these value references after copying a colelction of values. 
    member Attributes: ReadOnlyCollection<FSharpAttribute>

      /// XML documentation signature for the value.
    member XmlDocSig: string;

     
#if TODO
    /// Indicates if this is "base" in "base.M(...)"
    member IsBaseValue : bool

    /// Indicates if this is the "x" in "type C() as x = ..."
    member IsConstructorThisValue : bool

    /// Indicates if this is the "x" in "member x.M = ..."
    member IsMemberThisValue : bool

    /// Indicates if this is a [<Literal>] value, and if so what value?
    member LiteralValue : obj // may be null

      /// Get the module, type or namespace where this value appears. For 
      /// an extension member this is the type being extended 
    member ApparentParent: FSharpEntity

     /// Get the module, type or namespace where this value is compiled
    member ActualParent: FSharpEntity;

#endif

      /// How visible is this? 
    member Accessibility : FSharpAccessibility

    static member op_Equality : FSharpMemberOrVal * FSharpMemberOrVal -> bool
    static member op_Inequality : FSharpMemberOrVal * FSharpMemberOrVal -> bool


and [<Sealed>] FSharpParameter =
    member Name: string
    member DeclarationLocation : SourceLocation; 
    member Type : FSharpType; 
    member Attributes: ReadOnlyCollection<FSharpAttribute>
    static member op_Equality : FSharpParameter * FSharpParameter -> bool
    static member op_Inequality : FSharpParameter * FSharpParameter -> bool


and [<Sealed>] FSharpType =

    /// Indicates the type is constructed using a named entity
    member IsNamed : bool
    /// Get the named entity for a type constructed using a named entity
    member NamedEntity : FSharpEntity 
    /// Get the generic arguments for a tuple type, a function type or a type constructed using a named entity
    member GenericArguments : ReadOnlyCollection<FSharpType>
    
    /// Indicates the type is a tuple type. The GenericArguments property returns the elements of the tuple type.
    member IsTuple : bool

    /// Indicates the type is a function type. The GenericArguments property returns the domain and range of the function type.
    member IsFunction : bool

    /// Indicates the type is a variable type, whether declared, generalized or an inference type parameter  
    member IsGenericParameter : bool
    /// Get the generic parameter data for a generic parameter type
    member GenericParameter : FSharpGenericParameter
    /// Get the index for a generic parameter type
    member GenericParameterIndex : int

    static member op_Equality : FSharpType * FSharpType -> bool
    static member op_Inequality : FSharpType * FSharpType -> bool



and [<Sealed>]
    FSharpAttribute = 
        member Value : obj  
        
        member ReflectionType: System.Type 
        static member op_Equality : FSharpAttribute * FSharpAttribute -> bool
        static member op_Inequality : FSharpAttribute * FSharpAttribute -> bool



