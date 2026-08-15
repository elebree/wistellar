FROM gradle:8.13-jdk17 AS build

# Set the working directory
WORKDIR /app

# Install Android SDK
RUN mkdir -p /opt/android-sdk/cmdline-tools/latest && cd /opt/android-sdk/cmdline-tools/latest \
    && wget https://dl.google.com/android/repository/commandlinetools-linux-9477386_latest.zip -O android-sdk.zip \
    && unzip android-sdk.zip -d /opt/android-sdk \
    && rm android-sdk.zip

# Set ANDROID_HOME environment variable
ENV ANDROID_HOME=/opt/android-sdk
ENV PATH="$ANDROID_HOME/cmdline-tools/bin:$ANDROID_HOME/platform-tools:$PATH"

# Accept licenses and install required Android SDK packages
RUN yes | sdkmanager --sdk_root=$ANDROID_HOME --licenses && \
    sdkmanager --sdk_root=$ANDROID_HOME "platform-tools" "platforms;android-34" "build-tools;34.0.0"

# Set work directory
WORKDIR /app