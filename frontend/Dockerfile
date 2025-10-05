FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# preview (static) как просили
FROM node:20-alpine AS preview
WORKDIR /app
COPY --from=build /app/dist ./dist
RUN npm i serve
EXPOSE 5173
CMD ["npx","serve","-s","dist","-l","5173"]
