# Frontier Atlas · Atlas de Fronteras

Juego personal de estrategia territorial para Windows, desarrollado en C# con Unity 6 y Universal Render Pipeline. Autor del proyecto: Jesús Leonardo Martínez Rodrigues.

## Experiencia

Creación de una nación sobre un mapa terrestre, expansión territorial, producción económica, ciudades, fábricas, rutas comerciales, diplomacia y combate contra oponentes controlados por el juego. La campaña incorpora guardado y carga versionados.

## Diseño técnico

```text
Entrada / interfaz -> comandos -> simulación canónica -> visualización
                                      |
                       economía / diplomacia / combate
                                      |
                              guardado versionado
```

- El estado del mundo se representa con una cuadrícula e índices; no se crea un GameObject por celda.
- Simulación, cámara, interfaz y representación visual se mantienen separadas.
- Los sistemas de economía, política, comercio y combate se organizan en módulos C#.
- Los recursos 3D incluyen modelos e instancias agrupadas; los originales del autor permanecen fuera de esta copia cuando no son necesarios para ejecutar el proyecto.
- La rama de campaña documentada utiliza esquema de guardado v5 con lectura de versiones anteriores.

## Abrir

1. Instalar Unity **6000.6.7f1** mediante Unity Hub.
2. Añadir esta carpeta como proyecto. Esperar a que se resuelvan los paquetes del manifiesto.
3. Abrir `Assets/Scenes/FrontierAtlas.unity` y ejecutar Play.

## Estructura

`Assets/RoFrontPrototype/Scripts` contiene los sistemas, `Editor` los validadores y construcción, y `Shaders` la presentación. `Assets/UserModels` contiene recursos derivados. `Packages/manifest.json` fija las dependencias y `ProjectSettings` la configuración del editor.

## Alcance

Proyecto en desarrollo. La campaña local está documentada; las exploraciones de red y partida rápida no se presentan como un multijugador público listo para producción. No se promete una tasa de cuadros universal. Los validadores del editor requieren Unity; publicar los archivos no equivale a volver a compilar o probar una partida completa.

La copia publicable conserva el estado de los archivos locales en la fecha de preparación, incluidas modificaciones todavía no incorporadas a la historia original. No modifica el checkout de trabajo.

## Recursos y distribución

Se preservan avisos y licencias presentes. Las referencias a IA dentro del juego describen el comportamiento de los oponentes. El ejecutable Windows se entrega como paquete independiente cuando el canal de publicación permite su tamaño; no se incluye en el control de fuentes. Sin cachés de Unity, credenciales ni herramientas de integración del editor.
