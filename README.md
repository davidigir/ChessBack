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

## Propuestas de Mejora

- [ ] **Sistema de Espectadores**
- [ ] **Sistema de Skins**
- [ ] **Optimización del Bot**
- [ ] **Sistema de Red Social**
- [ ] **Análisis Post-Partida:**



## Diagramas

### Diagrama Entidad-Relación
<img width="449" height="361" alt="Chess drawio" src="https://github.com/user-attachments/assets/6a58a8b0-ce6b-411c-84d7-736deecbb99f" />


### Diagrama de flujo

<img width="3522" height="8192" alt="Mermaid Chart - Create complex, visual diagrams with text -2026-01-18-091035" src="https://github.com/user-attachments/assets/53951cae-f186-4c23-8f77-603262c3c1ca" />

