# WebAppApi

API REST ASP.NET Core 8 con SQLite, Docker y 12 endpoints, más un servidor TCP en el puerto `6061`.

## Endpoints

1. `GET /health` - estado de la API y conectividad con SQLite (`200` saludable, `503` no saludable)
2. `GET /api/categories` - listar categorías
3. `POST /api/categories` - crear categoría
4. `DELETE /api/categories/{id}` - eliminar categoría sin productos asociados
5. `GET /api/products` - listar productos
6. `POST /api/products` - crear producto
7. `GET /api/products/{id}` - consultar producto
8. `PUT /api/products/{id}` - actualizar producto
9. `DELETE /api/products/{id}` - eliminar producto
10. `POST /api/database/backup` - guardar una copia en `backups/`
11. `GET /api/database/backup/download` - descargar la base SQLite como archivo `.db`
12. `DELETE /api/database` - vaciar productos y categorías

Las respuestas tienen esta forma:

```json
{
  "statusCode": 200,
  "data": []
}
```

La base está normalizada en dos tablas: `Categories` y `Products`, relacionadas por `CategoryId`.

## Ejecutar con Docker

Desde la raíz del proyecto:

```powershell
docker build -t webapp:latest .
docker rm -f webapp-container 2>$null
docker run -d -p 8080:80 -p 6061:6061 --name webapp-container -v webapp-data:/app/data -v webapp-backups:/app/backups webapp:latest
```

Abrir `http://localhost:8080/swagger` o `http://localhost:8080/api/categories`.

Los volúmenes conservan la base SQLite y los respaldos aunque el contenedor se
detenga o se vuelva a crear.

Para detener y eliminar el contenedor:

```powershell
docker stop webapp-container
docker rm webapp-container
```

## Ejemplos de peticiones

Crear categoría:

```json
POST /api/categories
{"name":"Electrónica"}
```

Crear producto:

```json
POST /api/products
{"name":"Teclado","description":"USB","price":25.50,"categoryId":1}
```

Backup:

```text
POST /api/database/backup
```

Descargar la base de datos:

```text
GET /api/database/backup/download
```

En Swagger, ejecuta este endpoint y selecciona la opción para descargar la
respuesta. El archivo descargado tendrá extensión `.db`.

## Socket TCP

El mismo contenedor escucha conexiones TCP en el puerto `6061`. Cada solicitud
debe terminar con un salto de línea y el servidor responde con JSON.

Insertar un producto:

```text
{insert:{"name":"Mouse","description":"USB","price":250.00,"categoryId":1}}
```

Consultar un producto por ID:

```text
{get:1}
```

También se acepta el ID como objeto:

```text
{get:{"id":1}}
```

Desde WSL puedes probarlo con `netcat`:

```bash
printf '%s\n' '{get:1}' | nc 127.0.0.1 6061
```

## Docker Hub

Reemplaza `USUARIO_DOCKERHUB` por tu usuario:

```powershell
docker login
docker tag webapp:latest USUARIO_DOCKERHUB/webapp:latest
docker push USUARIO_DOCKERHUB/webapp:latest

# Verificar que la etiqueta local apunta a la imagen que se va a publicar
docker image inspect USUARIO_DOCKERHUB/webapp:latest --format '{{.Id}}'
```

## EC2 Ubuntu

Instalar Docker:

```bash
sudo apt update
sudo apt install -y docker.io
sudo systemctl enable --now docker
sudo usermod -aG docker $USER
```

Después de volver a iniciar sesión en SSH:

```bash
docker pull USUARIO_DOCKERHUB/webapp:latest
docker rm -f webapp-container 2>/dev/null || true
docker volume create webapp-data
docker volume create webapp-backups
docker run -d --restart unless-stopped -p 8080:80 -p 6061:6061 --name webapp-container \
  -v webapp-data:/app/data -v webapp-backups:/app/backups \
  USUARIO_DOCKERHUB/webapp:latest
```

En el Security Group de EC2 agrega reglas TCP personalizadas para los puertos
`8080` y `6061` desde tu IP. Prueba con
`http://IP_PUBLICA_EC2:8080/swagger` y conecta al socket con la IP pública y
puerto `6061`.

Ec2
