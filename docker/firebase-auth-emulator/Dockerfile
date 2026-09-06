# Firebase Authentication emulator (part of the Firebase Local Emulator Suite).
# Only the Auth emulator is started. It is pure JavaScript, so no Java runtime is needed.
FROM node:22-alpine

ARG FIREBASE_TOOLS_VERSION=15.29.0
RUN npm install -g "firebase-tools@${FIREBASE_TOOLS_VERSION}" \
    && firebase setup:emulators:ui \
    && npm cache clean --force

WORKDIR /srv/firebase
COPY firebase.json entrypoint.sh ./
RUN chmod +x entrypoint.sh

# 9099: Auth emulator (REST API + SDK endpoint), 4000: Emulator UI
EXPOSE 9099 4000

# Any project id starting with "demo-" keeps the emulator fully offline: no Firebase project,
# no credentials, and no calls to Google.
ENV FIREBASE_PROJECT=demo-asmpt

ENTRYPOINT ["./entrypoint.sh"]
