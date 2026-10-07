.PHONY: up down reset logs ps test test-backend test-engine benchmark-engine lint build migration database-update clean

up:
	docker compose up --build -d

down:
	docker compose down --remove-orphans

reset:
	docker compose down --remove-orphans --volumes

logs:
	docker compose logs -f

ps:
	docker compose ps

test: test-backend test-engine lint

test-backend:
	dotnet test apps/backend/tests/Abadar.Backend.Tests/Abadar.Backend.Tests.csproj

test-engine:
	dotnet test apps/matching-engine/tests/Abadar.MatchingEngine.Tests/Abadar.MatchingEngine.Tests.csproj

benchmark-engine:
	dotnet run --configuration Release --project apps/matching-engine/benchmarks/Abadar.MatchingEngine.Benchmarks/Abadar.MatchingEngine.Benchmarks.csproj

lint:
	cd apps/web && npm run lint && npm run typecheck

build:
	dotnet build apps/backend/src/Abadar.Backend/Abadar.Backend.csproj --configuration Release
	dotnet build apps/matching-engine/src/Abadar.MatchingEngine/Abadar.MatchingEngine.csproj --configuration Release
	cd apps/web && npm run build

migration:
	dotnet ef migrations add $(name) --project apps/backend/src/Abadar.Backend/Abadar.Backend.csproj --startup-project apps/backend/src/Abadar.Backend/Abadar.Backend.csproj --output-dir Data/Migrations

database-update:
	dotnet ef database update --project apps/backend/src/Abadar.Backend/Abadar.Backend.csproj --startup-project apps/backend/src/Abadar.Backend/Abadar.Backend.csproj

clean:
	rm -rf apps/backend/src/Abadar.Backend/bin apps/backend/src/Abadar.Backend/obj
	rm -rf apps/backend/tests/Abadar.Backend.Tests/bin apps/backend/tests/Abadar.Backend.Tests/obj
	rm -rf apps/matching-engine/src/Abadar.MatchingEngine/bin apps/matching-engine/src/Abadar.MatchingEngine/obj
	rm -rf apps/matching-engine/tests/Abadar.MatchingEngine.Tests/bin apps/matching-engine/tests/Abadar.MatchingEngine.Tests/obj
	rm -rf apps/matching-engine/benchmarks/Abadar.MatchingEngine.Benchmarks/bin apps/matching-engine/benchmarks/Abadar.MatchingEngine.Benchmarks/obj
	rm -rf apps/web/.next apps/web/node_modules
