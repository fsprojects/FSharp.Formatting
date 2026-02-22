
## Building and Development

- Clone the repository
- Build with `dotnet build`
- Open `FSharp.Formatting.sln` with Visual Studio or Visual Studio Code (with the ionide-fsharp extension)

## Source Formatting

This repository uses the Fantomas source code formatter and this is checked on commit.

Run

    dotnet fantomas src tests docs build.fsx -r

to format the code you've written.
