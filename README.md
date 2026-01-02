# Chess Backend

Servidor para una web de ajedrez en línea desarrollado con **.NET 9**. Gestiona la persistencia de usuarios, el cálculo de ranking y la lógica de partidas en tiempo real.

---

## Características principales

* **Juego en Tiempo Real:** Comunicación bidireccional mediante **SignalR**.
* **Sistema de ELO:** Cálculo dinámico de ranking tras cada partida finalizada.
* **Seguridad:** Autenticación con **JWT** protegida mediante Cookies.
* **Gestión de Partidas:** * Creación de salas públicas y privadas con contraseña.
    * Sistema de revanchas automáticas.
    * Detección de desconexión con temporizadores de reconexión.
    * Limpieza automática de memoria para salas inactivas.
* **Historial:** Registro detallado de movimientos y estadísticas por jugador.

---

## Tecnologías

* **Framework:** .NET 9 (ASP.NET Core Web API)
* **Base de Datos:** SQL Server + Entity Framework Core
* **Real-time:** SignalR
* **Seguridad:** JWT + BCrypt
