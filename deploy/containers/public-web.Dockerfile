FROM node:24.19.0-alpine AS dependencies
WORKDIR /app
COPY src/Frontend/PublicWeb/package.json src/Frontend/PublicWeb/package-lock.json ./
RUN npm ci

FROM node:24.19.0-alpine AS build
WORKDIR /app
COPY --from=dependencies /app/node_modules ./node_modules
COPY src/Frontend/PublicWeb .
RUN npm run build

FROM node:24.19.0-alpine AS final
WORKDIR /app
ENV NODE_ENV=production
COPY --from=build /app/public ./public
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
USER node
EXPOSE 3000
CMD ["node", "server.js"]
