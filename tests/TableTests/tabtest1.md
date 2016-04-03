# Tutorial 1: types, values and lists

## Content

| Syntax | Name | Description|
|---------|---------|------|
|`let` *name* [`:`*typename*] `=` *exp* | let binding | Defines identifier *name* to be equal to *exp* <br> for all subsequent expressions in current block<br> *typename* is optional because types will<br>be inferred by compiler.|
| `let `*fname* *p*` = `*exp* | function <br> let binding | Defines function *fname* with parameter *p* <br>to be equal to expression *exp* which may contain *p*.
| `let `*fname* `(`*p*` : `*typename*`) = `*exp* | as above | Types can be added to any parameters as shown
| `( `*e1*` , `*e2*`, `...` , `*en*` )` | n-tuple value | *ei* can be arbitrary expressions. <br> Like a record but with unnamed elements <br> and no type definition
| `[ `*e1*` ; `...` ; `*en* `]` | list | All the *ei* must be the same type. <br>List elements are separated by `;` not `,`.
| `[ `*e1*`..`*e2*` ]` | |List of all integers from *e1* to *e2*.<br> *e1*,*e2* must be *int*
| `fun `*p1*` -> `*e1*| anonymous<br> function | Like `let `*name* *p1*` = `*e1*
| `(` *op* `)`| functionised<br> operator | *op* must be a binary operator.<br>`(` *op* `) a b` is equivalent to `a` *op* `b`.<br> Mostly used to feed operators to <br>list processing functions|
|-------------|------|--------------------|

|Type| Description|
|---------|-----------|
| `a'` | *polymorphic* type variable can fit any specific type
| `int` <br> `float` <br> `string`<br> `bool`| Basic types
| *t1*` -> `*t2*|Type of function with parameter type *t1* and result type *t2*
| *t1*` * `*t2* | Type of 2-tuple with first element type *t1*, second type *t2*.
|-------------------|--------------|------------|

|Op|Function|Type|Meaning|
|--------|----|----|------|
|`+`<br> `-`<br> `*`<br> `/`| |<p class = "text-center"> `float->float->float` <br> or <br> `int->int->int` | arithmetic
| `**`| |`float->float->float` | power operator
| | `List.map` <br> `List.map f lst`| `('a->'b') -> 'a list -> 'b list`| map `f` over `lst`
| | `List.reduce`<br>`List.reduce f lst`| `('a->'a->'a) -> 'a list -> 'a` | reduce list to value using `f`
| | `List.filter`<br> `list.filter f lst` | `('a->bool)-> 'a list -> 'a list`| filter `lst` to contain only elements<br> for which `f` returns true
| `|>` | | `'a -> ('a -> 'b)` | Pipeline operator: `x |> f` = `f x`
|----------------|--------------|---------------|----------------|



