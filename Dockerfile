FROM eiriktsarpalis/dotnet-sdk-mono:3.1.101-buster

WORKDIR /app
COPY . .

CMD ./build.sh Bundle
