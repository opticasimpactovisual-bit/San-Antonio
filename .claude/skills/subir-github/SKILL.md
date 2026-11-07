---
name: subir-github
description: Sube a GitHub todos los cambios del proyecto San-Antonio (commit y push a main). Usar cuando el usuario pida "subir a GitHub", "subir todo", "subeme todo al github", "guardar en GitHub" o "subir los cambios".
---

# Subir cambios a GitHub

El usuario trabaja directamente en la rama `main` del repositorio
https://github.com/opticasimpactovisual-bit/San-Antonio. Habla en español y no es
programador: explícale el resultado con palabras sencillas.

## Pasos

1. Revisa qué cambió, incluidos los archivos dentro de carpetas nuevas:
   ```bash
   git status --short -uall
   git diff --stat
   ```
   Si no hay cambios, dile al usuario que todo ya está subido y termina.

2. Mira el contenido de los archivos nuevos o modificados antes de agregarlos
   (`git diff` para los modificados; lee los nuevos). No subas sin preguntar
   primero:
   - contraseñas o claves (revisa sobre todo `appsettings.json` y las cadenas de
     conexión: `Password=` no debe ir escrito; `Trusted_Connection=True` sí está bien),
   - archivos `.env`, temporales o de respaldo,
   - archivos de más de 50 MB,
   - las carpetas `Servidor/bin/` y `Servidor/obj/` (ya están en `.gitignore`).

3. Revisa que git sepa quién firma los cambios:
   ```bash
   git config user.name; git config user.email
   ```
   Si falta alguno, usa el mismo autor de los commits anteriores, solo para este
   repositorio:
   ```bash
   git log -1 --format="%an <%ae>"
   git config user.name "<nombre>"; git config user.email "<correo>"
   ```

4. Agrega los archivos por nombre (no uses `git add -A` a ciegas) y haz el commit
   con un mensaje corto en español que describa el cambio, por ejemplo
   "Cambiar campos de fecha a formato fecha".

5. Sube a GitHub:
   ```bash
   git push origin main
   ```
   Si el push falla porque GitHub tiene cambios que no están en el computador,
   ejecuta `git pull --rebase origin main` y vuelve a intentar. Si aparece un
   conflicto, detente y explícale al usuario qué archivo tiene el conflicto.
   Si falla por usuario o contraseña de GitHub, dile que inicie sesión en GitHub
   desde VS Code o con Git Credential Manager y vuelva a pedirlo.

6. Confirma con `git status --short` que no quedó nada pendiente y dile al usuario
   en español qué archivos se subieron (con una frase de qué cambió en cada uno),
   el mensaje del commit y el enlace al repositorio.
