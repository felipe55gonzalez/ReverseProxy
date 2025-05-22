# Proxy Reverso

Bienvenido al proyecto **Proxy Reverso**. Esta aplicación actúa como una **puerta de enlace (API Gateway)** robusta y configurable, construida con **.NET** y **YARP (Yet Another Reverse Proxy)**, diseñada para gestionar, asegurar y monitorear el acceso a tus servicios backend.

---

## 🚀 Características Principales

- **Enrutamiento Dinámico**  
  Configuración de rutas y clústeres de YARP gestionada desde una base de datos. No es necesario redesplegar para aplicar cambios.

- **Autenticación y Autorización Granular**  
  - Validación de tokens de API.  
  - Permisos basados en grupos de endpoints.  
  - Control de métodos HTTP permitidos por token y grupo.  
  - Gestión del estado y expiración de tokens.

- **Seguridad**  
  - Bloqueo de direcciones IP.  
  - Gestión dinámica de orígenes CORS.

- **Logging y Auditoría Detallados**  
  - Registro exhaustivo de solicitudes/respuestas (`RequestLogs`).  
  - Registro de eventos de auditoría (`AuditLogs`).

- **Monitorización y Análisis**  
  - Tabla de resumen horario de tráfico (`HourlyTrafficSummary`).  
  - Métricas de tamaño, geolocalización, etc.

- **Configurabilidad Total**  
  Todo se gestiona desde la base de datos `ProxyDB`.

---

## ⚙️ Configuración Inicial

### 1. Base de Datos

- **Nombre:** `ProxyDB`
- **Script de creación:** `ProxyDB-CreationQuery.sql` (incluido en el repositorio)

Ejecuta este script en tu SQL Server para crear la base de datos y sus tablas.

### 2. Datos Iniciales Requeridos

Debes poblar al menos las siguientes tablas:

#### 🧩 `EndpointGroups`
- `GroupName`: Nombre del grupo (ej. `ServiciosAMP`)  
- `PathPattern`: Ruta (ej. `/api/amp/{**remainder}`)  
- `MatchOrder`: Prioridad de coincidencia  
- `ReqToken`: Si requiere token (1 o 0)

#### 🛰️ `BackendDestinations`
- `Address`: URL del servicio backend  
- `FriendlyName`, `IsEnabled`, `HealthCheckPath` (opcional)

#### 🔗 `EndpointGroupDestinations`
Relaciona `EndpointGroups` con `BackendDestinations`.

#### 🔐 `ApiTokens`
- `TokenValue`, `Description`, `OwnerName`, `IsEnabled`, `DoesExpire`, `ExpiresAt`

#### 📜 `TokenPermissions`
- Relación `TokenId` ↔ `GroupId`  
- Métodos permitidos: `AllowedHttpMethods` (ej. `"GET,POST"`)

#### 🌍 `AllowedCorsOrigins`
- `OriginUrl`, `IsEnabled`

> ⚠️ Sin estos datos, el proxy no sabrá enrutar solicitudes o podría bloquearlas incorrectamente.

### 3. Configuración de la Aplicación

En `appsettings.json`:

- **Connection Strings:**  
  Asegúrate de que `ConnectionStrings:ProxyDB` apunta correctamente a tu base de datos.
  
- **Kestrel (Puertos):**  
  Define los puertos HTTP/HTTPS. Usa `*` o tu IP específica para acceso remoto.

- **Certificados SSL/TLS:**  
  - Producción: Certificado válido (via Kestrel o balanceador).  
  - Desarrollo: Usa `dotnet dev-certs https --trust`.

---

## 🧪 Casos de Uso

### 🔗 Puerta de Enlace Única
- Una sola URL pública para múltiples microservicios.
- Abstracción de la arquitectura interna.

### 🛡️ Seguridad Centralizada
- Validación de tokens y permisos.
- Bloqueo de IPs.
- Gestión de CORS.

### ⚖️ Balanceo de Carga
- Varios destinos para un `EndpointGroup`.

### 🔒 Descarga SSL/TLS
- Terminación SSL en el proxy para redes internas HTTP.

### 🧭 Enrutamiento Avanzado
- Basado en rutas (`PathPattern`).
- *(Futuro)* Basado en encabezados, HTTP verbs, etc.

### 📊 Logging y Monitorización
- Logging detallado.
- Auditar configuraciones y eventos.
- Tabla `HourlyTrafficSummary` para dashboards.

### 🔄 Transformaciones
- Modificación de headers (`X-Forwarded-*`, `Authorization`, etc).
- *(Futuro)* Transformación de cuerpos.

### 🔁 Resiliencia
- HealthChecks para evitar backends caídos.

---

## 🤝 Contribuir

¡Las contribuciones son bienvenidas! Puedes:

- **Reportar errores:** Abre un [issue](https://github.com/felipe55gonzalez/ReverseProxy/issues) describiendo el problema, pasos y entorno.
- **Sugerir mejoras:** Proponlo en un nuevo *issue*.
- **Enviar Pull Requests:** Correcciones, mejoras o nuevas funcionalidades son bienvenidas.

---

## 📄 Licencia

Este proyecto se distribuye bajo la **Licencia MIT**. Consulta el archivo `LICENSE` para más información.
