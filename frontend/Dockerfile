FROM node:24-bookworm-slim AS build

WORKDIR /source/frontend

COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend ./

ARG VITE_APP_ENV
ARG VITE_DATA_SOURCE
ARG VITE_API_URL
ARG VITE_ENABLE_DEMO_LOGIN
ARG VITE_ENABLE_AI
ARG VITE_ENABLE_NOTIFICATIONS

ENV VITE_APP_ENV=${VITE_APP_ENV} \
    VITE_DATA_SOURCE=${VITE_DATA_SOURCE} \
    VITE_API_URL=${VITE_API_URL} \
    VITE_ENABLE_DEMO_LOGIN=${VITE_ENABLE_DEMO_LOGIN} \
    VITE_ENABLE_AI=${VITE_ENABLE_AI} \
    VITE_ENABLE_NOTIFICATIONS=${VITE_ENABLE_NOTIFICATIONS}

RUN npm run build

FROM node:24-bookworm-slim AS runtime

ENV NODE_ENV=production \
    FRONTEND_PORT=3001

WORKDIR /app

COPY --from=build --chown=node:node /source/frontend/.output /app/frontend
COPY --chown=node:node frontend/gateway*.mjs /app/

USER node

EXPOSE 3000

CMD ["node", "/app/gateway.mjs"]
