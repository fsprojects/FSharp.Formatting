Diagnostics and debugging
=========================

The F# Formatting library has an extensive logging to help developers diagnose
potential issues. If you encounter any issues with F# Formatting, this
page gives you all the information you need to create a log file with detailed
trace of what is going one. This may give you some hints on what is wrong & a
detailed report that you can send when [submitting an
issue](https://github.com/tpetricek/FSharp.Formatting/issues).

Setting up logging
------------------

Logging is enabled when you reference F# Formatting using the `FSharp.Formatting.fsx`
load script. If you're not using the load script, you can enable logging manually
(just see [how this is done in the load script](https://github.com/tpetricek/FSharp.Formatting/blob/master/packages/FSharp.Formatting/FSharp.Formatting.fsx)).

By default, F# Formatting logs some information to the console output. 
Detailed logging is enabled by setting an environment variable `FSHARP_FORMATTING_LOG` to 
`ALL`. The log file `FSharp.Formatting.svclog` will be placed in the directory from where 
you run the tool. If you are using [ProjectScaffold](https://github.com/fsprojects/ProjectScaffold), 
then this is the folder from where you run the `build.sh` or `build.cmd` script.

You can set `FSHARP_FORMATTING_LOG` as follows:

 - `NONE` - Disable all logging. F# Formatting will not print anything to the console
   and it will also not produce a log file (this is not recomended, but you might need
   this if you want to suppress all output).
 - `ALL` - Enables detailed logging to a file `FSharp.Formatting.svclog` and keeps 
   printing of basic information to console too.
 - `FILE_ONLY` - Enables detailed logging to a file `FSharp.Formatting.svclog` but disables
   printing of basic information to console.
 - Any other value (default) - Print basic information to console and do not produce
   a detailed log file.

Enabling logging on Windows
---------------------------

On Windows, you can set environment variables by going to system properties
(this varies depending on the OS version, but generally right click on
"My Computer" and select a link or button saying something like "Change settings").

This should open a new dialog, where you can go to "Advanced", and click on the
"Environment Variables" button. Here, you can add the variable as either per-user
or per-system and save it. 

Enabling logging on Mac/Linux
-----------------------------

If you're using Linux or Mac, then the easiest option is to set the
variable from Terminal and then start either Xamarin Studio or the build script from terminal. Note that
if you set the environment variable from terminal, but launch Xamarin Studio
from Dock or in some other way, it will not see the variable!

The following should do the trick (assuming the folder `/Users/tomasp/Temp` exists):

    [lang=text]
    export FSHARP_FORMATTING_LOG=ALL
    open -n /Applications/Xamarin\ Studio.app/

This will set the variable and start a new instance of Xamarin Studio in the current
context. Once it appears, reporduce the operation that causes the error, close
Xamarin Studio and look at the log file.
