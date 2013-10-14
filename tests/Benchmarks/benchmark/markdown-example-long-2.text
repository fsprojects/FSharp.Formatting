**Original answer:** I'm  not sure if you will like how mathematical courses typically introduce matrices. As a programmer you might be happier with grabbing any decent 3D graphics book. It should certainly have very concrete 3x3 matrices. Also find out the ones that will teach you [projective transformations][1] (projective geometry is a very beautiful area of low-dimensional geometry and easy to program).

Mini-course in matrix math with Python 3
==

**Contents:**

 1. **Matrices** `[Vector, __add__, reflect_y, rotate, dilate, transform]`
 1. **Matrices: Overloaded** `[Matrix, __add__, __str__, __mul__, zero, det, inv, __pow__]`
 1. **Bonus: Complex numbers**
 1. **Matrices: The (R)evolution**. It's already in the making (there's a summary at the end)

**Preface:** Based on my teaching experience, I think that the courses referenced by others are very good *courses*. That means if your goal is understanding matrices as mathematicians do, than you should by all means get the whole course. But if your goals are more modest, here's my try at something more tailored to your needs (but still written with the goal to convey many theoretical concepts, kind of contradicting my original advice.)
   
**How to use:**

* This post is long. You might consider printing this and going slow, like one part a day.
* Code is essential. This is a course for programmers. Exercises are essential too. 
* You should **[take a look at the code companion][2]** which contains all this code and much more
* It's "2 for the price of 1" special: you can also learn Python 3 here. And complex numbers.
* I'll highly value any attempt to read this (do I officially qualify for the longest post ever?), so feel free to comment if you don't understand something (and also if you do).

1. Matrices
=

Vectors
-

Before matrices come vectors. You sure know how to handle the 2- and 3- dimensional vectors:

    class Vector:
        """This will be a simple 2-dimensional vector.
        
        In case you never encountered Python before, this string is a
        comment I can put on the definition of the class or any function.
        It's just one of many cool features of Python, so learn it here!
        
        """
        
        def __init__(self, x, y): 
            self.x = x
            self.y = y
    
now you can write

    v = Vector(5, 3)
    w = Vector(7, -1)

but it's not much fun by itself. Let's add more useful methods:

        def __str__(self: 'vector') -> 'readable form of vector':
            return '({0}, {1})'.format(self.x, self.y)
            
        def __add__(self:'vector', v: 'another vector') -> 'their sum':
            return Vector(self.x + v.x, self.y + v.y)
        
        def __mul__(self:'vector', number: 'a real number') -> 'vector':
            '''Multiplies the vector by a number'''
            return Vector(self.x * number, self.y * number)
        
        
That makes things more interesting as we can now write:
    
    print(v + w * 2)

and get the answer `(19, 1)` nicely printed as a vector (if the examples look unfamiliar, think how this code would look in C++). 

Tranformations
-

Now it's all cool to be able to write `1274 * w` but you need more vector operations for the graphics. Here are some of them: you can flip the vector around `(0,0)` point, you can reflect it around `x` or `y` axis, you can rotate it clockwise or counterclockwise (it's a good idea to draw a picture here). 

Let's do some simple operations:

        ...

        def flip(self:'vector') -> 'vector flipped around 0':
            return Vector(-self.x, -self.y)
        
        def reflect_x(self:'vector') -> 'vector reflected around x axis':
            return Vector(self.x, -self.y)
    

    print(v.flip(), v.reflect_x())

* **Question:** is it possible to express `flip(...)` using the operations I had below? What about `reflect_x`?

Now you may wonder why I omitted `reflect_y`. Well, it's because I want you to stop for a moment and write your own version of it. Ok, here's mine:

        def reflect_y(self:'vector') -> 'vector reflected around y axis':
            return self.flip().reflect_x()

See, if you look how this function computes, it's actually quite trivial. But suddenly an amazing thing happened: I was able to write a transformation using only the existing transformations `flip` and `reflect_x`. For all I care, `reflect_y` could be defined in a derived class without access to `x` and `y` and it would still work!

Mathematicians would call these functions *operators*. They would say that `reflect_y` is an operator obtained by *composition* of operators `flip` and `reflect_x` which is
denoted by `reflect_y = flip ? reflect_x` (you should see the small circle, a Unicode symbol `25CB`).

* **Note:** I will quite freely use the `=` symbol from now to denote that two operations produce the same result, like in the paragraph above. This is a "mathematical `=`", which [cannot be expressed as a program][3].

So if I do 

    print(v.reflect_y())

I get the result `(-5, 3)`. Go and picture it!

* **Question:** Consider a composition `reflect_y ? reflect_y`. How would you name it? 

Rotations
--

Those operations were nice and useful, but you are probably wondering why am so slow to introduce rotations. Ok, here I go:

        def rotate(self:'vector', angle:'rotation angle') -> 'vector':
            ??????

At this point, if you know how to rotate vectors, you should go on and fill in the question marks. Otherwise please bear with me for one more simple case: counterclockwise rotation by `90` degrees. This one is not hard to draw on a piece of paper:

        def rotate_90(self:'vector') -> 'rotated vector':
            new_x = - self.y
            new_y =   self.x
            return Vector(new_x, new_y)


Trying 

    x_axis = Vector(1, 0)
    y_axis = Vector(0, 1)
    
    print(x_axis.rotate_90(), y_axis.rotate_90())

now gives `(0, 1) (-1, 0)`. Run it yourself!

* **Question:** Prove that `flip = rotate_90 ? rotate_90`.

Anyway, I won't hide the secret ingredient for longer:

    import math   # we'll need math from now on
      ...
    
    class Vector:

          ...

        def rotate(self:'vector', angle:'rotation angle') -> 'rotated vector':
            cos = math.cos(angle)
            sin = math.sin(angle)
            new_x = cos * self.x - sin * self.y
            new_y = sin * self.x + cos * self.y
            return Vector(new_x, new_y)

Now let's try something along the lines:

    print(x_axis.rotate(90), y_axis.rotate(90))
    
If you expect the same result as before, `(0, 1) (-1, 0)`, you're bound to be disappointed. That code prints:
 
    (-0.448073616129, 0.893996663601) (-0.893996663601, -0.448073616129)

and boy, is it ugly!

* **Notation:** I will say that we applied operation `rotate(90)` to `x` in the example above. The knowledge we gained is that `rotate(90) != rotate_90`.

* **Question:** What happened here? How to express `rotate_90` in terms of `rotate`?  How to express `flip` in terms of `rotate`?

Dilations
--

Those rotations are certainly useful, but they are not everything you need to do even the 2D graphics. Consider the following transformations: 

        def dilate(self:'vector', axe_x:'x dilation', axe_y:'y dilation'):
            '''Dilates a vector along the x and y axes'''
            new_x = axe_x * self.x
            new_y = axe_y * self.y
            return Vector(new_x, new_y)

This `dilate` thing dilates the `x` and `y` axes in a possibly different way. 

* **Exercise:** Fill in the question marks in `dilate(?, ?) = flip`, `dilate(?, ?) = reflect_x`.

I will use this `dilate` function to demonstrate a thing mathematicians call *commutativity*: that is, for every value of parameters `a`, `b`, `c`, `d` you can be sure that 
    
    dilate(a, b) ? dilate(c, d) = dilate(c, d) ? dilate(a, b)

* **Exercise:** Prove it. Also, is it true that for all possible values of parameters those below would hold?
    

    *      `rotate(a) ? rotate(b) = rotate(b) ? rotate(a)`
    *      `dilate(a, b) ? rotate(c) = rotate(c) ? dilate(a, b)`
    *      `rotate(a) ? __mul__(b) = __mul__(b) ? rotate(a)`

Matrices
-

Let's summarize all the stuff we had around here, our *operators on vector `x`*

* `flip`, `reflect_x`, `*`, `rotate(angle)`, `dilate(x, y)`

from which one could make some really crazy stuff like 

* `flip ? rotate(angle) ? dilate(x, y) ? rotate(angle_2) ? reflect_y + reflect_x = ???`

As you create more and more complicated expressions, one would hope for some kind of order that would suddenly reduce all possible expressions to a useful form. Fear not! Magically, every expression of the form above can be simplified to

        def ???(self:'vector', parameters):
            '''A magical representation of a crazy function'''
            new_x = ? * self.x + ? * self.y
            new_y = ? * self.x + ? * self.y
            return Vector(new_x, new_y)

with some numbers and/or parameters instead of `?`s.

* **Example:** Work out what the values of '?' are for `__mul__(2) ? rotate(pi/4)`
* **Another example:** Same question for  `dilate(x, y) ? rotate(pi/4)`

This allows us to write a universal function

        def transform(self:'vector', m:'matrix') -> 'new vector':
            new_x = m[0] * self.x + m[1] * self.y
            new_y = m[2] * self.x + m[3] * self.y
            return Vector(new_x, new_y)
 
which would take any 4-tuple of numbers, called **matrix**, and *apply* it to vector `x`. Here's an example:

    rotation_90_matrix = (0, -1, 1, 0)
    print(v, v.rotate_90(), v.transform(rotation_90_matrix))

which prints `(5, 3) (-3, 5) (-3, 5)`. Note that if you apply `transform` with
any matrix to origin, you still get origin:

    origin = Vector(0, 0)
    print(origin.transform(rotation_90_matrix))

* **Exercise:** what are the tuples `m` that describe `flip`, `dilate(x, y)`, `rotate(angle)`?

As we part with the `Vector` class, here's an exercise for those who want to test both their vector math knowledge and Pythonic skills:


* **The final battle:** Add to the `Vector` class all vector operations that you can come up with (how many of standard operators can you overload for vectors? Check out my answer).


2. Matrices: Overloaded
=

As we found out in the previous section, a matrix can be thought of a shorthand that allows us to encode a vector operation in a simple way. For example, `rotation_90_matrix` encodes the rotation by 90 degrees.

Matrix objects
-

Now as we shift our attention from vectors to matrices, we should by all means have a class
for matrix as well. Moreover, in that function `Vector.transform(...)` above the role of the matrix was somewhat misrepresented. It's more usual for `m` to be fixed while vector changes, so from now on our transformations will be methods of matrix class:


    class Matrix:

        def __init__(self:'new matrix', m:'matrix data'):
            '''Create a new matrix.
        
            So far a matrix for us is just a 4-tuple, but the action
            will get hotter once The (R)evolution happens!
            
            '''
            self.m = m

        def __call__(self:'matrix', v:'vector'):
            new_x = self.m[0] * v.x + self.m[1] * v.y
            new_y = self.m[2] * v.x + self.m[3] * v.y
            return Vector(new_x, new_y)


If you don't know Python, `__call__` overloads the meaning of `(...)` for matrices so I can use the standard notation for a matrix *acting* on a vector. Also, the matrices are usually written using a single uppercase letter:

    J = Matrix(rotation_90_matrix)
    print(w, 'rotated is', J(w))

* **Exercise:** repeat this example with matrices from the previous exercise. 


Addition
-

Now, let's find out what else we can do with matrices. Remember that matrix `m` is really just a way to encode an operaton on vectors. Note that for  two functions `m1(x)` and `m2(x)` I can create a new function (using [lambda notation][4]) `m = lambda x: m1(x) + m2(x)`. It turns out if `m1` and `m2` were enconded by matrices, *you can also encode this `m` using matrices*!

* **Exercise:** Think through any difficulties you might have with this statement.

You just have to add its data, like `(0, 1, -1, 0) + (0, 1, -1, 0) = (0, 2, -2, 0)`. Here's how to add two tuples in Python, with some very useful and highly Pythonic techniques:

        def __add__(self:'matrix', snd:'another matrix'):
            """This will add two matrix arguments.
            
            snd is a standard notation for second argument.
            (i for i in array) is Python's powerful list comprehension.
            zip(a, b) is used to iterate over two sequences together
            
            """
            
            new_m = tuple(i + j for i, j in zip(self.m, snd.m))
            return Matrix(new_m)

Now we can write expressions like `J + J` or even `J + J + J`, but to see the results we have to figure out how to print a Matrix. A possible way would be to print a 4-tuple of numbers, but let's take a hint from the `Matrix.__call__` function that the numbers should be organized into a `2x2` block:

        def as_block(self:'matrix') -> '2-line string':
            """Prints the matrix as a 2x2 block.

            This function is a simple one without any advanced formatting.
            Writing a better one is an exercise.
                        
            """

            return ('| {0} {1} |\n' .format(self.m[0], self.m[1]) +
                    '| {0} {1} |\n' .format(self.m[2], self.m[3]) )

If you look at this function in action you'll notice there is some room for improvement:

    print((J + J + J).as_block())

* **Exercise:** write a nicer function `Matrix.__str__` that will round the
numbers and print them in the fields of fixed length.

Now you should be able to write the matrix for rotation:

    def R(a: 'angle') -> 'matrix of rotation by a':
        cos = math.cos(a)
        sin = math.sin(a)
        m = ( ????? )
        return Matrix(m)
    
* **Exercise:** Examine the code for `Vector.rotate(self, angle)` and fill in the question marks. Test with

        from math import pi        
        print(R(pi/4) + R(-pi/4))

Multiplication
-

The most important thing we can do with one-parameter functions is compose them: `f = lambda v: f1(f2(v))`. How to mirror that with matrices? This requires us to examine how `Matrix(m1) ( Matrix(m2) (v))` works. If you expand it, you'll notice that 
    
    m(v).x = m1[0] * (m2[0]*v.x + m2[1]*v.y) + m1[1] * (m2[2]*v.x + m2[3]*v.y)

and similarly for `m(v).y`, which, if you open the parentheses, looks suspiciously similar
to `Matrix.__call__` using a new tuple `m`, such that `m[0] = m1[0] * m2[0] + m1[2] * m2[2]`. So let's take this as a hint for a new definiton:

        def compose(self:'matrix', snd:'another matrix'):
            """Returns a matrix that corresponds to composition of operators"""
            
            new_m = (self.m[0] * snd.m[0] + self.m[1] * snd.m[2],
                     self.m[0] * snd.m[1] + self.m[1] * snd.m[3],
                     ???,
                     ???) 
            return Matrix(new_m)

* **Exercise:** Fill in the question marks here. Test it with

        print(R(1).compose(R(2)))
        print(R(3))

* **Math exercise:** Prove that `R(a).compose(R(b))` is always the same as `R(a + b)`.

Now let me tell the truth: this `compose` function is actually how mathematicians decided to *multiply* matrices.
This makes sense as a notation: `A * B` is a matrix that decribes operator `A ? B`, and as we'll see next there are deeper reasons to call this 'multiplication' as well.

To start using multiplication in Python all we have to do is to order it so in a `Matrix`
class:

        class Matrix:
        
              ...
        
            __mul__ = compose
            
* **Exercise:** Compute `(R(pi/2) + R(pi)) * (R(-pi/2) + R(pi))`. Try to find the answer yourself first on a piece of paper.

Rules for `+` and `*`
-

Let's make some good name for the matrix that corresponds to the `dilate(a, b)` operator. Now there's nothing wrong with `D(a, b)`, but I'll
use a chance to introduce a standard notation:

    def diag(a: 'number', b: 'number') -> 'diagonal 2x2 matrix':
        m = (a, 0, 0, b)
        return Matrix(m)
        
Try `print(diag(2, 12345))` to see why it's called a *diagonal* matrix. 

As the composition of operations was found before to be not always commutative, `*` operator won't be always commutative for matrices either. 

* **Exercise:** go back and refresh the *commutativity* thing if necessary. Then give examples of matrices `A`, `B`, made from `R` and `diag`,
such that `A * B` is not equal to `B * A`.

This is somewhat strange, since multiplication for numbers is always commutative, and raises the question whether `compose` really deserves to be called `__mul__`. Here's quite a lot of rules that `+` and `*`  *do* satisfy:

 1. `A + B = B + A`
 1. `A * (B + C) = A * B + A * C`
 2. `(A + B) * C = A * C + B * C`
 3. `(A * B) * C = A * (B * C)`
 4. There is an operation called `A - B` and `(A - B) + B = A`

* **Exercise:** Prove these statements. How to define `A - B` in terms of `+`, `*` and `diag`? What does `A - A` equal to? Add the method `__sub__` to the class `Matrix`. What happens if you compute `R(2) - R(1)*R(1)`? What should it be equal to?

The `(A * B) * C = A * (B * C)` equality is called *associativity* and is especially nice since it means that we don't have to worry about putting parentheses in an expression
of the form `A * B * C`:

    print(R(1) * (diag(2,3) * R(2)))
    print((R(1) * diag(2,3)) * R(2))

Let's find analogues to regular numbers `0` and `1` and subtraction:

    zero = diag(0, 0)
    one = diag(1, 1)     

With the following easily verifiable additions:
 
 1. `A + zero = A`
 2. `A * zero = zero`
 3. `A * one = one * A = A`

the rules become complete, in the sense that there is a short name for them: *ring axioms*.
Mathematicians thus would say that matrices form a *ring*, and they indeed always use symbols `+` and `*` when talking about rings, and so shall we.

Using the rules it's possible to easily compute the expression from the previous section:
     
    (R(pi/2) + R(pi)) * (R(-pi/2) + R(pi)) = R(pi/2) * R(-pi/2) +  ... = one + ...

* **Exercise:** Finish this. Prove that `(R(a) + R(b)) * (R(a) - R(b)) = R(2a) - R(2b)`. 


Affine Transformations
-

Time to return to how we defined matrices: they are a shortcut to some operations you can do with vectors, so it's something you can actually draw. You might want to take a pen or look at the materials that others suggested to see examples of different plane transformations.

Among the transformations we'll be looking for the *affine* ones, those who look 'the same' everywhere (no bending). For example, a rotation around some point `(x, y)` qualifies. Now this one cannot be expressed as `lambda v: A(v)`, but in can be written in the form `lambda v: A(v) + b` for some matrix `A` and vector `b`. 

* **Exercise:** find the `A` and `b` such that a rotation by `pi/2` around the point `(1, 0)` has the form above. Are they unique?


Note that for every vector there is an affine transformation which is a *shift* by the vector. 
    
An affine transformation may stretch or dilate shapes, but it should do in the same way everywhere. Now I hope you believe that the area of any figure changes by a constant number under the transformation. For a transformation given by matrix `A` this coeffiecient is called the *determinant* of `A` and can be computed applying the formula for an area to two vectors `A(x_axis)` and `A(y_axis)`:

        def det(self: 'matrix') -> 'determinant of a matrix':
            return self.m[0]*self.m[3] - self.m[1] * self.m[2]

As a sanity check, `diag(a, b).det()` is equal to `a * b`.

* **Exercise:** Check this. What happens when one of arguments is 0? When it's negative?

As you can see, the determinant of rotation matrix is always the same:

    from random import random
    r = R(random())
    print (r, 'det =', r.det())
    
One interesting thing about `det` is that it is multiplicative (it kind of follows from the definition if you meditate long enough):
    
    A = Matrix((1, 2, -3, 0))
    B = Matrix((4, 1, 1, 2))
    print(A.det(), '*', B.det(), 'should be', (A * B).det())
    

Inverse
-
A useful thing you can do with matrices is write a system of two linear equations
    
    A.m[0]*v.x + A.m[1]*v.y = b.x
    A.m[2]*v.x + A.m[3]*v.y = b.y

in a simpler way: `A(v) = b`. Let's solve the system as they teach in (some) high schools: multiply first equation by `A.m[3]`, second by -A.m[1] and add (if in doubt, do this on a piece of paper) to solve for `v.x`.

If you really tried it, you should have got `A.det() * v.x = (A.m[3]) * b.x + (-A.m[1]) * b.y`, which suggests that you can always get `v` by multiplying `b` by some other matrix. This matrix is called *inverse* of `A`:

        def inv(self: 'matrix') -> 'inverse matrix':
            '''This function returns an inverse matrix when it exists,
            or raises ZeroDivisionError when it doesn't. 
            
            '''
            new_m = ( self.m[3] / self.det(), -self.m[1] / self.det(),
                      ????? )
            return Matrix(new_m)

As you see, this method fails loudly when determinant of matrix is zero. If you really want you can catch this expection with:

    try:
        print(zero.inv())
    except ZeroDivisionError as e: ...

* **Exercise:** Finish the method. Prove that inverse matrix doesn't exist when `self.det() == 0`. Write the method to divide matrices and test it. Use the inverse matrix to solve an equation `A(v) = x_axis` (`A` was defined above).


Powers
-

The main property of inverse matrix is that `A * A.inv()` always equals to `one`

* **Exercise:** check that yourself. Explain why that should be so from the definition of inverse matrix.

That's why mathematicians denote `A.inv()` by `A`<sup>-1</sup>. How about we write a
nice function to use `A ** n` notation for `A`<sup>n</sup>? Note that the naive `for i in range(n): answer *= self` cycle is O(|n|) which is certainly too slow, because
this can be done with a  complexity of `log |n|`:

        def __pow__(self: 'matrix', n:'integer') -> 'n-th power':
            '''This function returns n-th power of the matrix.
            
            It does it more efficiently than a simple for cycle. A
            while loop goes over all bits of n, multiplying answer
            by self ** (2 ** k) whenever it encounters a set bit.
            
            ...
            
* **Exercise:** Fill in the details in this function. Test it with

    `X, Y = A ** 5, A ** -5`
    `print (X, Y, X * Y, sep = '\n')`

This function only works for integer values of `n`, even though for some matrices we can also define a fractional power, such as square root (in other words, a matrix `B` such that `B * B = A`).


* **Exercise:** Find a square root of `diag(-1, -1)`. Is this the only possible answer? 
Find an example of matrix that *doesn't* have a square root.


Bonus: Complex numbers
=

Here I'm going to introduce you to the subject in exactly one section!
Since it's a complex subject, I'm likely to fail, so please forgive me in advance.

First, similarly to how we have matrices `zero` and `one`, we can make a matrix out of any real number by doing `diag(number, number)`. Matrices of that form can be added, subtracted, multiplied, inverted and the results would mimic what happens with the numbers themselves. So for all practical purposes, one can say that, e.g., `diag(5, 5)` *is* 5.

However, Python doesn't know yet how to handle expressions of the form `A + 1` or `5 * B` where `A` and `B` are matrices. If you're interested, you should by all means go and do the following exercise or look at my implementation (which uses a cool Python feature called *decorator*); otherwise, just know that it's been implemented.

* **Exercise for gurus:** Change the operators in a `Matrix` class so that in all standard operations where one of operands is a matrix and another a number, the number is automatically converted to the `diag` matrix. Also add comparison for equality.

Here's an example test:

    print( 3 * A - B / 2 + 5 )

Now here's the first interesting **complex number**: the matrix `J`,  introduced in the beginning and equal to `Matrix((0, 1, -1, 0))`, has a funny property that `J * J == -1` (try it!). That means `J` is certainly not a normal number, but, as I just said, matrices and numbers easily mix together. For example,
    
    (1 + J) * (2 + J) == 2 + 2 * J + 1 * J + J * J = 1 + 3 * J

using the rules listed some time before. What happens if we test this in Python?

    (1 + J) * (2 + J) == 1 + 3*J
    
That should happily say `True`. Another example:

    (3 + 4*J) / (1 - 2*J) == -1 + 2*J 

As you might have guessed, the mathematicians don't call those 'crazy numbers', but they do something similar - they call expressions of the form `a + b*J` *complex numbers*.
Because those are still instances of our `Matrix` class, we can do quite a lot of operations with those: addition, subtraction, multiplication, division, power - it's all already implemented! Aren't matrices amazing?

I have overlooked the question of how to print the result of operation like ` E = (1 + 2*J) * (1 + 3*J)` so that it looks like an expression with `J` rather than a `2x2` matrix. If you examine it carefully,
you'll see that you need to print the left column of that matrix in the format `... + ...J` (just one more nice thing: it's exactly `E(x_axis)`!) Those who know the difference between `str()` and `repr()` should see it's natural to name a function that would produce expression of such form as `repr()`.

* **Exercise:** Write the function `Matrix.__repr__` that would do exactly that and try some tests with it, like `(1 + J) ** 3`, first computing the result on paper and then trying it with Python.

* **Math question:** What is the determinant of `a + b*J`? If you know what the *absolute value* of complex number is: how they are connected? What is the absolute value of `a`? of `a*J`?



3. Matrices: The (R)evolution
=

In the final part of this trilogy we will see that everything is a matrix. We'll start with general `M x N` matrices, and find out how vectors can be thought of as `1 x N` matrices and why numbers are the same as diagonal matrices. As a side note we'll explore the complex numbers as `2 x 2` matrices.

Finally, we will learn to write affine and projective transformations using matrices.

So the classes planned are `[MNMatrix, NVector, Affine, Projective]`.

I guess if you was able to bear with me until here, you could be interested in this sequel, so I'd like to hear if I should continue with this (and where, since I'm pretty much sure I'm beyond what considered reasonable length of a single document).


  [1]: http://en.wikipedia.org/wiki/Projective_transformations
  [2]: http://mit.edu/~unknot/www/matrices.py
  [3]: http://en.wikipedia.org/wiki/List_of_undecidable_problems
  [4]: http://en.wikipedia.org/wiki/Lambda_calculus