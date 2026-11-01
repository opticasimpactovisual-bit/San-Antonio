---
name: subir-github
description: Sube a GitHub todos los cambios del proyecto San-Antonio (commit y push a main). Usar cuando el usuario pida "subir a GitHub", "guardar en GitHub" o "subir los cambios".
---

# Subir cambios a GitHub

El usuario trabaja directamente en la rama `main` del repositorio
https://github.com/opticasimpactovisual-bit/San-Antonio. Habla en español.

## Pasos

1. Revisa qué cambió:
   ```bash
   git status --short
   git diff --stat
   ```
   Si no hay cambios, dile al usuario que todo ya está subido y termina.

2. Mira los archivos nuevos o modificados antes de agregarlos. No subas archivos
   temporales, contraseñas, archivos `.env` ni archivos muy grandes (más de 50 MB)
   sin preguntar primero.

3. Agrega los archivos por nombre (no uses `git add -A` a ciegas) y haz el commit
   con un mensaje corto en español que describa el cambio, por ejemplo
   "Cambiar campos de fecha a formato fecha".

4. Sube a GitHub:
   ```bash
   git push origin main
   ```
   Si el push falla porque GitHub tiene cambios que no están en el computador,
   ejecuta `git pull --rebase origin main` y vuelve a intentar. Si aparece un
   conflicto, detente y explícale al usuario qué archivo tiene el conflicto.

5. Confirma con `git status --short` que no quedó nada pendiente y dile al usuario
   en español qué archivos se subieron, el mensaje del commit y el enlace al
   repositorio.
