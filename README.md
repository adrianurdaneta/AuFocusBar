# AuTaskBar (FocusBar)

Barra flotante para Windows (WPF, .NET 10) enfocada en productividad diaria: reloj dual, tarea principal editable, estado Pomodoro y acceso rápido a configuración.

## Características principales

- Barra minimalista siempre visible (`Topmost` opcional).
- Fecha y hora para dos zonas (`Madrid` y `Venezuela`) con indicador día/noche.
- Tarea central con edición inline (clic sobre el texto para editar, `Enter` para guardar).
- Línea secundaria para próxima reunión.
- Bloque de Pomodoro con:
  - estado visual,
  - tiempo restante,
  - barra de progreso.
- Menú contextual en XAML con comandos y atajos de acciones comunes.
- Ventana de configuración para tema, tamaño/opacidad de barra y parámetros de Pomodoro.
- Icono en bandeja del sistema con acciones rápidas.

## Tecnologías

- `.NET 10`
- `WPF`
- `WPF-UI` (estilos/recursos visuales)

## Estructura del proyecto

- `AuTaskBar/MainWindow.xaml` y `MainWindow.xaml.cs`: UI principal y lógica de interacción.
- `AuTaskBar/SettingsWindow.xaml` y `SettingsWindow.xaml.cs`: configuración.
- `AuTaskBar/Services/`
  - `FocusBarSettings.cs`: modelo de configuración,
  - `SettingsService.cs`: persistencia en JSON,
  - servicios auxiliares de tareas/calendario.

## Ejecutar en local

### Requisitos

- SDK de `.NET 10`
- Windows con soporte WPF

### Build

```powershell
dotnet build
```

### Run

```powershell
dotnet run --project AuTaskBar/AuTaskBar.csproj
```

## Configuración persistida

La configuración se guarda en `%AppData%/AuTaskBar/settings.json`.

Incluye, entre otros:

- tamaño y opacidad de barra,
- tema principal/modo,
- minutos de focus/rest de Pomodoro,
- ejecución al iniciar Windows,
- estado de fijado (`Topmost`).

## Notas

- El menú contextual de barra y bandeja está unificado.
- Los cambios de tema están orientados al menú contextual y `SettingsWindow`; el color principal impacta los elementos visuales de la FocusBar y detalles de Settings.
