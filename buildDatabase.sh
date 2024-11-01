cd src/DokkanDailyDB
docker stop mydatabase:1.0 || true && docker rm mydatabase:1.0 || true
docker build . --build-arg PASSWORD="<YourStrong@Passw0rd>" -t mydatabase:1.0 --no-cache
docker run -p 1433:1433 --name sqldb -d mydatabase:1.0