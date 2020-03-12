FROM eiriktsarpalis/dotnet-sdk-mono:3.1.101-buster

# allow pushing docs & tags using docker builds:
# pass git credentials using $GITHUB_TOKEN environment variable
ARG GIT_ASKPASS=/root/.git-askpass
ENV GIT_ASKPASS=$GIT_ASKPASS
RUN echo 'echo $GITHUB_TOKEN' > $GIT_ASKPASS && \
	chmod +x $GIT_ASKPASS

# configure git name & email using params
ARG GIT_USER_NAME
ARG GIT_USER_EMAIL
RUN git config --global user.name "$GIT_USER_NAME" && \
	git config --global user.email "$GIT_USER_EMAIL"

WORKDIR /app
COPY . .

CMD ./build.sh
