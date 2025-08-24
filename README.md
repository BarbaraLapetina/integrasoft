# Pasos para traer la rama experimento a repositorio local

1. Primero entramos a la terminal en el directorio integrasoft que es el repo local de main

2. Luego abrimos la terminal y ejecutamos los siguientes comandos (yo por comodidad uso la terminal de visual studio code)
git fetch origin
git checkout -b experimento origin/experimento

### Para confirmar que ya estas en la rama
git branch


### Si querés realizar un cambio sobre la rama experimento

git add .
git commit -m "mensaje"
git push origin experimento
