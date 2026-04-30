# TelcoPilot — frontend Dockerfile (Next.js standalone)
# Build context: ./frontend
# syntax=docker/dockerfile:1.7
#
# `# syntax=` directive on line 3 enables BuildKit cache mounts. Compose builds
# already use BuildKit by default (Docker Desktop / Buildx), so no extra env var
# is needed.

FROM node:22-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json* ./

# Why npm ci (not npm install): the lockfile is committed, so we want a deterministic
# install that skips dependency-tree re-resolution entirely. The previous `npm install`
# hit ECONNRESET inside Docker on Windows after ~9 min — the long-running stream
# of registry fetches outlasted Docker Desktop's NAT idle window.
#
# The fetch-retry / timeout bumps protect against the same NAT drops on big tarballs:
# default fetch-timeout is 60 s with only 2 retries, which is too tight for a fresh
# Next.js 15 + MobX install over the network. A cache mount on /root/.npm lets re-runs
# pick up tarballs from a persistent BuildKit volume instead of re-downloading.
RUN --mount=type=cache,target=/root/.npm \
    npm config set fetch-retries 5 && \
    npm config set fetch-retry-mintimeout 20000 && \
    npm config set fetch-retry-maxtimeout 120000 && \
    npm config set fetch-timeout 600000 && \
    npm ci --legacy-peer-deps --no-audit --no-fund --prefer-offline

FROM node:22-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
ENV NEXT_TELEMETRY_DISABLED=1
RUN npm run build

FROM node:22-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV PORT=3000

# Standalone output bundles the minimum runtime + node_modules into .next/standalone
COPY --from=build /app/public ./public
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static

EXPOSE 3000
CMD ["node", "server.js"]
