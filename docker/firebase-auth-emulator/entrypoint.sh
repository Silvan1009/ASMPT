#!/bin/sh
# Starts the Firebase Auth emulator.
# Accounts are imported from the export directory when a previous export exists there
# (written on shutdown via --export-on-exit, persisted in a volume), otherwise from the checked-in seed.
set -eu

# The export lives in a subdirectory of the volume because the emulator deletes and recreates
# the export directory, which fails with EBUSY on the mount point itself.
EXPORT_DIR=/srv/firebase/data/export
SEED_DIR=/srv/firebase/seed
IMPORT_DIR=""

if [ -f "$EXPORT_DIR/firebase-export-metadata.json" ]; then
  IMPORT_DIR="$EXPORT_DIR"
elif [ -f "$SEED_DIR/firebase-export-metadata.json" ]; then
  IMPORT_DIR="$SEED_DIR"
fi

set -- emulators:start --only auth --project "${FIREBASE_PROJECT:-demo-asmpt}" --export-on-exit "$EXPORT_DIR"
if [ -n "$IMPORT_DIR" ]; then
  set -- "$@" --import "$IMPORT_DIR"
fi

exec firebase "$@"
