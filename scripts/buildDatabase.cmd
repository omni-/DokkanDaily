cd ../src/DokkanDailyDB
docker image rm -f mydatabase:1.0 || cd .
docker build . --build-arg PASSWORD="<YourStrong@Passw0rd>" -t mydatabase:1.0 --no-cache
docker run -p 1433:1433 --name sqldb -d mydatabase:1.0
cd ../..