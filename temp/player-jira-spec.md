# NBA PLAYER PROP PROJECTIONS - JIRA SPECIFICATION

**Project**: Moneyball Sports Betting System  
**Module**: Player Layer & ML Extension  
**Version**: 1.0  
**Date**: February 2026  
**Scope**: 3-6 months part-time development

-----

## EXECUTIVE SUMMARY

This specification extends the existing NBA sports betting system with comprehensive player-level data ingestion and machine learning capabilities for player prop projections. The system will ingest player profiles, season statistics, and game-by-game performance data from SportRadar API, store them relationally in SQL Server, and train multiple ML models per stat category to predict player performance.

**Key Deliverables:**

- Player data layer (Players, PlayerSeasonStats, PlayerGameStats tables)
- Player data ingestion pipeline
- ML feature engineering framework
- Multi-model training and comparison system
- Prediction API endpoints
- Comprehensive test coverage

-----

## EPIC 1: PLAYER DATA LAYER FOUNDATION

**Epic ID**: MBALL-300  
**Story Points**: 21  
**Priority**: Critical  
**Dependencies**: Existing Teams, Games tables

### Story 1.1: Design and Implement Player Database Schema

**Story ID**: MBALL-301  
**Story Points**: 8  
**Priority**: Critical  
**Acceptance Criteria**:

- Players table created with all profile fields
- PlayerSeasonStats table with unique constraints
- PlayerGameStats table with game-level stats
- MLPlayerModels table for model metadata
- All foreign key relationships defined
- Indexes optimized for prop queries
- Computed columns for PRA, double-doubles, fantasy scores

**Tasks**:

- [ ] Create Players table with profile fields (height, weight, position, etc.)
- [ ] Add unique constraint on ExternalPlayerId
- [ ] Create PlayerSeasonStats table with season aggregations
- [ ] Add unique constraint on (PlayerId, Season, TeamId)
- [ ] Create PlayerGameStats table with game-by-game stats
- [ ] Add unique constraint on (PlayerId, GameId)
- [ ] Add computed columns for IsDoubleDouble, IsTripleDouble
- [ ] Add computed column for DraftKingsFantasyScore
- [ ] Create indexes on PlayerId + GameId DESC for rolling averages
- [ ] Create index on GameId for boxscore queries
- [ ] Create filtered index excluding DNPs for feature generation
- [ ] Create MLPlayerModels table for model tracking
- [ ] Add index on (StatCategory, IsBestModel)
- [ ] Write migration script with rollback
- [ ] Document schema in technical wiki

-----

### Story 1.2: Create Player Entity Classes and DTOs

**Story ID**: MBALL-302  
**Story Points**: 5  
**Priority**: Critical  
**Acceptance Criteria**:

- Player, PlayerSeasonStat, PlayerGameStat entities match schema
- PlayerProfileDto maps to SportRadar player API format
- SeasonStatsDto handles averages and totals
- GameBoxscoreDto with nested player arrays
- PlayerGameStatsDto with all stat fields
- Navigation properties configured

**Tasks**:

- [ ] Create Player entity class with all fields
- [ ] Create PlayerSeasonStat entity with navigation properties
- [ ] Create PlayerGameStat entity with computed properties
- [ ] Create MLPlayerModel entity
- [ ] Create PlayerProfileDto for API ingestion
- [ ] Create SeasonStatsDto with nested objects
- [ ] Create GameBoxscoreDto structure
- [ ] Create PlayerGameStatsDto with statistics object
- [ ] Add XML documentation to all classes
- [ ] Configure EF Core entity mappings
- [ ] Add Data Annotations for validation
- [ ] Write unit tests for DTO mapping

-----

### Story 1.3: Implement Player Repository Interfaces

**Story ID**: MBALL-303  
**Story Points**: 5  
**Priority**: High  
**Acceptance Criteria**:

- IPlayerRepository with CRUD and query methods
- IPlayerGameStatsRepository with rolling average queries
- IMLPlayerModelRepository for model management
- Async methods throughout
- Unit of Work pattern integration

**Tasks**:

- [ ] Define IPlayerRepository interface
- [ ] Add GetByExternalIdAsync method
- [ ] Add GetActivePlayersAsync method
- [ ] Add GetPlayersByTeamAsync method
- [ ] Add GetWithStatsAsync for eager loading
- [ ] Define IPlayerGameStatsRepository interface
- [ ] Add GetPlayerGameStatsAsync(playerId, lastN) method
- [ ] Add GetGameBoxscoreAsync method
- [ ] Add GetRecentStatsForMLAsync method
- [ ] Define IMLPlayerModelRepository interface
- [ ] Add GetBestModelForStatAsync method
- [ ] Add SetBestModelAsync method
- [ ] Implement concrete repository classes
- [ ] Register repositories in DI container
- [ ] Write repository integration tests

-----

### Story 1.4: Build ML_PlayerFeatures View

**Story ID**: MBALL-304  
**Story Points**: 8  
**Priority**: High  
**Acceptance Criteria**:

- View generates rolling averages (L3, L5, L10)
- Season averages included
- Home/away splits calculated
- Rest days and back-to-back flag computed
- Opponent context available
- Excludes DNPs from features
- Performant for training queries

**Tasks**:

- [ ] Design CTE structure for window functions
- [ ] Implement GameContext CTE
- [ ] Implement RecentGames CTE with ROWS BETWEEN windowing
- [ ] Calculate L3 averages for all target stats
- [ ] Calculate L5 averages for all target stats
- [ ] Calculate L10 averages for all target stats
- [ ] Implement HomeSplits CTE
- [ ] Calculate home vs away performance
- [ ] Implement RestDays CTE with LAG function
- [ ] Add IsBackToBack calculation
- [ ] Join to PlayerSeasonStats for season averages
- [ ] Add opponent team context
- [ ] Add pace proxy (total score)
- [ ] Create indexes to support view queries
- [ ] Test query performance on 10K+ rows
- [ ] Document feature engineering logic

-----

### Story 1.5: Create Derived Stat Calculator Utility

**Story ID**: MBALL-305  
**Story Points**: 3  
**Priority**: Medium  
**Acceptance Criteria**:

- IsDoubleDouble method checks 2+ categories at 10+
- IsTripleDouble method checks 3+ categories at 10+
- CalculateRollingAverage handles variable N
- Excludes DNPs automatically
- Handles edge cases (insufficient data)

**Tasks**:

- [ ] Create DerivedStatCalculator class
- [ ] Implement IsDoubleDouble method
- [ ] Check Points, Rebounds, Assists, Steals, Blocks >= 10
- [ ] Implement IsTripleDouble method
- [ ] Implement CalculateRollingAverage method
- [ ] Add LINQ ordering by GameId DESC
- [ ] Add Take(N) for last N games
- [ ] Filter out DidNotPlay = true
- [ ] Handle empty result sets
- [ ] Write comprehensive unit tests with Theory tests
- [ ] Test edge cases (0 games, 1 game, etc.)
- [ ] Document calculation logic

-----

### Story 1.6: Build DraftKings Fantasy Calculator

**Story ID**: MBALL-306  
**Story Points**: 3  
**Priority**: Medium  
**Acceptance Criteria**:

- Correct point values: Pts(1), 3PM(0.5), Reb(1.25), Ast(1.5), Stl(2), Blk(2), TO(-0.5)
- Double-double bonus: +1.5
- Triple-double bonus: +3.0 (not +1.5)
- ProjectFantasyScore uses probability-weighted bonuses
- All calculations match DraftKings official scoring

**Tasks**:

- [ ] Create DraftKingsFantasyCalculator class
- [ ] Define point value constants
- [ ] Implement CalculateFantasyScore method
- [ ] Calculate base score from stats
- [ ] Add triple-double check and +3.0 bonus
- [ ] Add double-double check and +1.5 bonus (if not TD)
- [ ] Implement ProjectFantasyScore method
- [ ] Calculate expected value of bonuses
- [ ] Write unit tests for all scoring scenarios
- [ ] Test with real DraftKings examples
- [ ] Verify no double-counting of bonuses
- [ ] Document DraftKings scoring rules

-----

## EPIC 2: PLAYER DATA INGESTION PIPELINE

**Epic ID**: MBALL-310  
**Story Points**: 34  
**Priority**: Critical  
**Dependencies**: MBALL-300 (Player Data Layer)

### Story 2.1: Implement SportRadar Player API Integration

**Story ID**: MBALL-311  
**Story Points**: 8  
**Priority**: Critical  
**Acceptance Criteria**:

- GetNBAPlayersAsync fetches all active players
- GetPlayerProfileAsync retrieves individual profile
- GetPlayerSeasonStatsAsync fetches season aggregates
- GetGameBoxscoreAsync retrieves full boxscore with all players
- Rate limiting implemented (1 request per second)
- Circuit breaker for API failures

**Tasks**:

- [ ] Add SportRadar player endpoints to SportsDataService
- [ ] Implement GetNBAPlayersAsync method
- [ ] Parse player list response
- [ ] Implement GetPlayerProfileAsync(playerId)
- [ ] Implement GetPlayerSeasonStatsAsync(playerId, season)
- [ ] Parse season stats with averages and totals
- [ ] Implement GetGameBoxscoreAsync(gameId)
- [ ] Parse home and away player arrays
- [ ] Add rate limiting (1 req/sec)
- [ ] Add circuit breaker pattern
- [ ] Add retry logic with exponential backoff
- [ ] Write integration tests with mock responses
- [ ] Document API endpoints and responses
- [ ] Add logging for all API calls

-----

### Story 2.2: Build Player Ingestion Service

**Story ID**: MBALL-312  
**Story Points**: 13  
**Priority**: Critical  
**Acceptance Criteria**:

- IngestNBAPlayersAsync upserts all players
- Idempotent - can run multiple times
- Updates existing players on profile changes
- IngestPlayerGameStatsAsync processes date range
- Creates or updates PlayerGameStat records
- Handles DNP cases correctly
- Transaction per game for data consistency

**Tasks**:

- [ ] Create PlayerIngestionService class
- [ ] Implement IngestNBAPlayersAsync method
- [ ] Fetch all players from SportRadar
- [ ] Check if player exists by ExternalPlayerId
- [ ] Create new player if not exists
- [ ] Update existing player profile
- [ ] Implement MapToPlayer helper method
- [ ] Implement UpdatePlayerFromProfile helper
- [ ] Add transaction and save changes
- [ ] Implement IngestPlayerGameStatsAsync method
- [ ] Fetch completed games in date range
- [ ] Fetch boxscore for each game
- [ ] Process home team players
- [ ] Process away team players
- [ ] Implement UpsertPlayerGameStatAsync
- [ ] Check if stat exists (PlayerId, GameId)
- [ ] Create or update PlayerGameStat
- [ ] Implement MapToPlayerGameStat helper
- [ ] Parse minutes string “MM:SS”
- [ ] Handle DidNotPlay flag and reason
- [ ] Add comprehensive logging
- [ ] Write unit tests with FluentAssertions
- [ ] Test idempotency (run twice, same result)

-----

### Story 2.3: Create Season Stats Aggregation Job

**Story ID**: MBALL-313  
**Story Points**: 8  
**Priority**: High  
**Acceptance Criteria**:

- Aggregates game stats into season averages
- Calculates double-double and triple-double counts
- Upserts PlayerSeasonStats records
- Handles mid-season trades (player on multiple teams)
- Runs nightly or on-demand

**Tasks**:

- [ ] Create SeasonStatsAggregator class
- [ ] Implement AggregateSeasonStatsAsync method
- [ ] Query all PlayerGameStats for season
- [ ] Group by PlayerId, Season, TeamId
- [ ] Calculate games played and started
- [ ] Calculate average points, rebounds, assists, etc.
- [ ] Calculate shooting percentages
- [ ] Count double-doubles where IsDoubleDouble = 1
- [ ] Count triple-doubles where IsTripleDouble = 1
- [ ] Calculate average DraftKings fantasy score
- [ ] Upsert PlayerSeasonStats record
- [ ] Handle unique constraint violations
- [ ] Add logging for aggregation results
- [ ] Write unit tests for aggregation logic
- [ ] Create scheduled job or endpoint

-----

### Story 2.4: Build Minutes Parser Utility

**Story ID**: MBALL-314  
**Story Points**: 2  
**Priority**: Medium  
**Acceptance Criteria**:

- Parses “MM:SS” format correctly
- Handles edge cases: “0:30”, “48:00”, empty, null, invalid
- Returns (minutes, seconds) tuple
- Robust error handling

**Tasks**:

- [ ] Create MinutesParser static class
- [ ] Implement ParseMinutes method
- [ ] Split on ‘:’ delimiter
- [ ] Parse minutes as int
- [ ] Parse seconds as int
- [ ] Return (0, 0) for invalid input
- [ ] Write Theory tests for all formats
- [ ] Test “32:15”, “0:30”, “48:00”
- [ ] Test empty string, null, “invalid”
- [ ] Document expected formats

-----

### Story 2.5: Implement DNP Handling Logic

**Story ID**: MBALL-315  
**Story Points**: 3  
**Priority**: Medium  
**Acceptance Criteria**:

- DidNotPlay flag set when player inactive
- DNPReason captured from API
- DNP games excluded from rolling averages
- DNP games excluded from season averages
- All statistics zero for DNP games

**Tasks**:

- [ ] Add DidNotPlay boolean field to entity
- [ ] Add DNPReason string field
- [ ] Parse “Played” flag from API
- [ ] Parse “Not_Playing_Reason” from API
- [ ] Set DidNotPlay = !dto.Played
- [ ] Set DNPReason from API field
- [ ] Set all stats to zero for DNP
- [ ] Add WHERE DidNotPlay = 0 to ML_PlayerFeatures view
- [ ] Add WHERE DidNotPlay = 0 to aggregation queries
- [ ] Test DNP handling in ingestion
- [ ] Verify DNP exclusion from features

-----

## EPIC 3: FEATURE ENGINEERING FRAMEWORK

**Epic ID**: MBALL-320  
**Story Points**: 21  
**Priority**: High  
**Dependencies**: MBALL-310 (Ingestion Pipeline)

### Story 3.1: Build Rolling Average Calculator

**Story ID**: MBALL-321  
**Story Points**: 5  
**Priority**: Critical  
**Acceptance Criteria**:

- Calculates L3, L5, L10 averages for all target stats
- Uses SQL window functions for performance
- Excludes DNP games
- Handles players with < N games gracefully
- Averages match manual calculations

**Tasks**:

- [ ] Extend ML_PlayerFeatures view
- [ ] Add Points_L3, Rebounds_L3, Assists_L3
- [ ] Add ThreePointsMade_L3, Minutes_L3
- [ ] Add Points_L5, Rebounds_L5, Assists_L5
- [ ] Add Points_L10, Rebounds_L10, Assists_L10
- [ ] Use ROWS BETWEEN N PRECEDING AND 1 PRECEDING
- [ ] Partition by PlayerId, order by GameId
- [ ] Filter DidNotPlay = 0 before windowing
- [ ] Test with players having < 10 games
- [ ] Verify averages match manual calculations
- [ ] Benchmark query performance
- [ ] Add indexes if needed

-----

### Story 3.2: Calculate Home/Away Splits

**Story ID**: MBALL-322  
**Story Points**: 3  
**Priority**: High  
**Acceptance Criteria**:

- Points_HomeAvg calculated for all home games
- Points_AwayAvg calculated for all away games
- Splits available for all target stats
- Current game IsHomeGame flag included

**Tasks**:

- [ ] Create HomeSplits CTE in view
- [ ] Calculate AVG(Points) WHERE IsHomeGame = 1
- [ ] Calculate AVG(Points) WHERE IsHomeGame = 0
- [ ] Partition by PlayerId for player-specific splits
- [ ] Add splits for Rebounds, Assists, 3PM
- [ ] Join to main query
- [ ] Include IsHomeGame flag from current game
- [ ] Test splits calculation
- [ ] Verify home/away differences

-----

### Story 3.3: Compute Rest Days and Back-to-Back

**Story ID**: MBALL-323  
**Story Points**: 3  
**Priority**: Medium  
**Acceptance Criteria**:

- DaysSinceLastGame calculated using LAG
- IsBackToBack = 1 when days <= 1
- Handles first game of season (NULL days)
- Feature available for all games

**Tasks**:

- [ ] Create RestDays CTE in view
- [ ] Use LAG(GameDate) OVER (PARTITION BY PlayerId ORDER BY GameId)
- [ ] Calculate DATEDIFF between current and previous game
- [ ] Set IsBackToBack = 1 when days <= 1
- [ ] Set IsBackToBack = 0 otherwise
- [ ] Handle NULL case (first game)
- [ ] Join to main query
- [ ] Test back-to-back detection
- [ ] Verify against actual schedules

-----

### Story 3.4: Add Opponent Defensive Stats

**Story ID**: MBALL-324  
**Story Points**: 5  
**Priority**: Medium  
**Acceptance Criteria**:

- OpponentTeamId identified from game context
- Join to opponent defensive averages vs position
- Defensive averages calculated per position
- Available as features for training

**Tasks**:

- [ ] Add OpponentTeamId to view (ternary based on IsHomeGame)
- [ ] Create OpponentDefense CTE
- [ ] Calculate opponent’s average points allowed by position
- [ ] Group by TeamId, OpponentPosition
- [ ] Calculate avg rebounds, assists allowed
- [ ] Join to main query on OpponentTeamId and PlayerPosition
- [ ] Handle NULL position gracefully
- [ ] Test defensive stats calculation
- [ ] Verify opponent context correctness

-----

### Story 3.5: Calculate Pace Proxy

**Story ID**: MBALL-325  
**Story Points**: 2  
**Priority**: Low  
**Acceptance Criteria**:

- TotalScore (HomeScore + AwayScore) included
- Acts as proxy for game pace
- Available for all completed games

**Tasks**:

- [ ] Add TotalScore to GameContext CTE
- [ ] Calculate HomeScore + AwayScore
- [ ] Include in main query as OpponentPace
- [ ] Test correlation with actual pace
- [ ] Document pace proxy usage

-----

### Story 3.6: Add Usage Proxy Placeholder

**Story ID**: MBALL-326  
**Story Points**: 3  
**Priority**: Low  
**Acceptance Criteria**:

- UsageRate column added with placeholder calculation
- Simple approximation based on FGA + FTA
- Documentation for future enhancement
- Available as feature

**Tasks**:

- [ ] Add UsageRate calculation to view
- [ ] Approximate as (FGA + 0.44 * FTA) / MinutesPlayed
- [ ] Include in feature set
- [ ] Document as approximation
- [ ] Add TODO for true usage rate formula
- [ ] Test calculation

-----

## EPIC 4: MULTI-MODEL ML FRAMEWORK

**Epic ID**: MBALL-330  
**Story Points**: 55  
**Priority**: Critical  
**Dependencies**: MBALL-320 (Feature Engineering)

### Story 4.1: Design Python ML Training Pipeline

**Story ID**: MBALL-331  
**Story Points**: 8  
**Priority**: Critical  
**Acceptance Criteria**:

- Python environment with scikit-learn, xgboost, lightgbm
- Training script accepts stat category as parameter
- Loads features from ML_PlayerFeatures view
- Splits data into train/validation/test
- Handles missing values
- Scales features appropriately

**Tasks**:

- [ ] Create Python virtual environment
- [ ] Install scikit-learn, xgboost, lightgbm, pandas, sqlalchemy
- [ ] Create train_player_model.py script
- [ ] Add CLI arguments: –stat-category, –model-type
- [ ] Implement load_features_from_sql function
- [ ] Connect to SQL Server via sqlalchemy
- [ ] Query ML_PlayerFeatures view
- [ ] Handle NULL values (imputation or drop)
- [ ] Split data: 70% train, 15% validation, 15% test
- [ ] Implement StandardScaler for features
- [ ] Save scaler with model
- [ ] Add logging configuration
- [ ] Write unit tests for data loading

-----

### Story 4.2: Implement Linear Regression Models

**Story ID**: MBALL-332  
**Story Points**: 5  
**Priority**: High  
**Acceptance Criteria**:

- LinearRegression from scikit-learn
- Trained per stat category
- MAE, RMSE, R2 calculated
- Model saved as pickle file
- Baseline for comparison

**Tasks**:

- [ ] Create LinearRegressionTrainer class
- [ ] Implement train method
- [ ] Fit LinearRegression on training data
- [ ] Predict on validation set
- [ ] Calculate MAE using mean_absolute_error
- [ ] Calculate RMSE using mean_squared_error
- [ ] Calculate R2 using r2_score
- [ ] Save model to /models/Points_LinearRegression_v1.pkl
- [ ] Log metrics
- [ ] Return model metadata dictionary
- [ ] Write unit tests

-----

### Story 4.3: Implement Random Forest Models

**Story ID**: MBALL-333  
**Story Points**: 8  
**Priority**: High  
**Acceptance Criteria**:

- RandomForestRegressor from scikit-learn
- Hyperparameter tuning via GridSearchCV
- Feature importance extracted
- Cross-validation performed
- Model saved with metadata

**Tasks**:

- [ ] Create RandomForestTrainer class
- [ ] Define hyperparameter grid (n_estimators, max_depth, min_samples_split)
- [ ] Implement train method with GridSearchCV
- [ ] Set cv=5 for 5-fold cross-validation
- [ ] Fit on training data
- [ ] Get best_estimator_ from grid search
- [ ] Extract feature_importances_
- [ ] Calculate validation metrics
- [ ] Calculate CV mean and std MAE
- [ ] Save best model
- [ ] Save feature importance as JSON
- [ ] Log hyperparameters and metrics
- [ ] Write unit tests

-----

### Story 4.4: Implement XGBoost Models

**Story ID**: MBALL-334  
**Story Points**: 8  
**Priority**: High  
**Acceptance Criteria**:

- XGBRegressor from xgboost
- Hyperparameter tuning
- Early stopping on validation set
- Feature importance
- Model saved

**Tasks**:

- [ ] Create XGBoostTrainer class
- [ ] Define hyperparameter grid (n_estimators, learning_rate, max_depth)
- [ ] Implement train method
- [ ] Use early_stopping_rounds=10
- [ ] Pass validation set as eval_set
- [ ] Fit XGBRegressor
- [ ] Get best_iteration
- [ ] Extract feature_importances_
- [ ] Calculate metrics
- [ ] Save model
- [ ] Log all hyperparameters
- [ ] Write unit tests

-----

### Story 4.5: Implement LightGBM Models

**Story ID**: MBALL-335  
**Story Points**: 8  
**Priority**: Medium  
**Acceptance Criteria**:

- LGBMRegressor from lightgbm
- Faster training than XGBoost
- Categorical feature handling
- Model saved

**Tasks**:

- [ ] Create LightGBMTrainer class
- [ ] Define hyperparameter grid
- [ ] Implement train method
- [ ] Handle categorical features (position, IsHomeGame)
- [ ] Fit LGBMRegressor
- [ ] Extract feature_importances_
- [ ] Calculate metrics
- [ ] Save model
- [ ] Compare speed to XGBoost
- [ ] Write unit tests

-----

### Story 4.6: Build Model Comparison Framework

**Story ID**: MBALL-336  
**Story Points**: 5  
**Priority**: High  
**Acceptance Criteria**:

- Trains all models for stat category
- Compares MAE, RMSE, R2
- Selects best model by MAE
- Logs comparison results
- Updates MLPlayerModels table

**Tasks**:

- [ ] Create ModelComparer class
- [ ] Implement compare_models_for_stat method
- [ ] Accept stat category parameter
- [ ] Train LinearRegression, RandomForest, XGBoost, LightGBM
- [ ] Collect metrics from each
- [ ] Sort by MAE ascending
- [ ] Select best model
- [ ] Log comparison table
- [ ] Save all models
- [ ] Mark best model in database
- [ ] Write unit tests

-----

### Story 4.7: Implement Model Persistence Service

**Story ID**: MBALL-337  
**Story Points**: 5  
**Priority**: High  
**Acceptance Criteria**:

- Saves models to /models directory
- Generates unique model names with version
- Stores metadata in MLPlayerModels table
- Loads models by ID or stat category
- Handles model versioning

**Tasks**:

- [ ] Create ModelPersistenceService class
- [ ] Implement save_model method
- [ ] Generate model filename: {Stat}_{Type}_v{Version}.pkl
- [ ] Save model file with pickle
- [ ] Insert record into MLPlayerModels table
- [ ] Include: ModelName, StatCategory, ModelType, Version
- [ ] Include: ModelFilePath, MAE, RMSE, R2Score
- [ ] Include: Hyperparameters as JSON
- [ ] Include: FeatureImportance as JSON
- [ ] Implement load_model method
- [ ] Load by ModelId
- [ ] Load best model for stat category
- [ ] Implement set_best_model method
- [ ] Update IsBestModel flags
- [ ] Write integration tests

-----

### Story 4.8: Create Cross-Validation Framework

**Story ID**: MBALL-338  
**Story Points**: 5  
**Priority**: Medium  
**Acceptance Criteria**:

- 5-fold cross-validation
- Time-series aware split (no future data in train)
- CV metrics saved (mean, std)
- Prevents overfitting

**Tasks**:

- [ ] Implement TimeSeriesSplit from scikit-learn
- [ ] Create CrossValidator class
- [ ] Implement evaluate_with_cv method
- [ ] Perform 5-fold time-series split
- [ ] Train model on each fold
- [ ] Predict on validation fold
- [ ] Calculate MAE per fold
- [ ] Calculate mean and std MAE
- [ ] Log CV results
- [ ] Save CVMeanMAE and CVStdMAE to database
- [ ] Write unit tests

-----

### Story 4.9: Build Hyperparameter Tuning Pipeline

**Story ID**: MBALL-339  
**Story Points**: 8  
**Priority**: Medium  
**Acceptance Criteria**:

- GridSearchCV for exhaustive search
- RandomizedSearchCV for large spaces
- Bayesian optimization option (optional)
- Best parameters saved with model
- Prevents overfitting

**Tasks**:

- [ ] Create HyperparameterTuner class
- [ ] Define parameter grids for each model type
- [ ] Implement tune_random_forest method
- [ ] Use GridSearchCV with cv=5
- [ ] Implement tune_xgboost method
- [ ] Use RandomizedSearchCV for large space
- [ ] Implement tune_lightgbm method
- [ ] Get best_params_ from search
- [ ] Refit on full training set
- [ ] Save hyperparameters as JSON
- [ ] Log tuning results
- [ ] Write unit tests

-----

## EPIC 5: PREDICTION API & INTEGRATION

**Epic ID**: MBALL-340  
**Story Points**: 34  
**Priority**: High  
**Dependencies**: MBALL-330 (ML Framework)

### Story 5.1: Create Python Inference Service

**Story ID**: MBALL-341  
**Story Points**: 8  
**Priority**: Critical  
**Acceptance Criteria**:

- predict.py script accepts player ID and game context
- Loads best model for stat category
- Generates features for upcoming game
- Returns predictions as JSON
- Handles missing models gracefully

**Tasks**:

- [ ] Create predict.py script
- [ ] Add CLI arguments: –player-id, –game-date, –opponent, –is-home
- [ ] Implement load_best_model function
- [ ] Query MLPlayerModels for IsBestModel = 1
- [ ] Load model pickle file
- [ ] Load associated scaler
- [ ] Implement generate_features_for_prediction
- [ ] Query player’s recent games
- [ ] Calculate rolling averages
- [ ] Get season averages
- [ ] Calculate rest days
- [ ] Get opponent defensive stats
- [ ] Scale features using saved scaler
- [ ] Predict with model
- [ ] Return JSON: {stat_category: predicted_value}
- [ ] Handle model not found error
- [ ] Write unit tests

-----

### Story 5.2: Build C# Prediction Controller

**Story ID**: MBALL-342  
**Story Points**: 8  
**Priority**: Critical  
**Acceptance Criteria**:

- GET /api/predictions/player/{playerId}
- Query parameters: gameDate, opponentTeamId, isHome
- Calls Python script via Process
- Parses JSON response
- Returns PlayerProjectionDto
- Handles errors gracefully

**Tasks**:

- [ ] Create PredictionsController in API project
- [ ] Add GetPlayerProjection endpoint
- [ ] Accept playerId, gameDate, opponentTeamId, isHome parameters
- [ ] Validate parameters
- [ ] Create PlayerProjectionDto
- [ ] Implement PythonInferenceService
- [ ] Use Process.Start to call predict.py
- [ ] Pass arguments: –player-id {id} –game-date {date}
- [ ] Read stdout JSON response
- [ ] Parse JSON to C# object
- [ ] Handle stderr errors
- [ ] Log Python execution
- [ ] Return 200 OK with predictions
- [ ] Return 404 if model not found
- [ ] Return 500 on Python error
- [ ] Write integration tests

-----

### Story 5.3: Implement Batch Prediction Endpoint

**Story ID**: MBALL-343  
**Story Points**: 5  
**Priority**: Medium  
**Acceptance Criteria**:

- POST /api/predictions/batch
- Accepts array of PlayerPredictionRequest
- Returns array of predictions
- Parallel processing
- Progress reporting

**Tasks**:

- [ ] Add BatchPrediction endpoint
- [ ] Accept List<PlayerPredictionRequest> in body
- [ ] Validate all requests
- [ ] Use Parallel.ForEach for concurrent predictions
- [ ] Call Python script for each request
- [ ] Collect all results
- [ ] Return List<PlayerProjectionDto>
- [ ] Add timeout per prediction
- [ ] Handle partial failures
- [ ] Log batch execution time
- [ ] Write integration tests

-----

### Story 5.4: Create Prediction Caching Layer

**Story ID**: MBALL-344  
**Story Points**: 5  
**Priority**: Medium  
**Acceptance Criteria**:

- Cache predictions for (PlayerId, GameDate, Opponent)
- TTL of 1 hour
- Redis or in-memory cache
- Cache invalidation on new data ingestion
- Reduces Python execution overhead

**Tasks**:

- [ ] Add IDistributedCache to DI
- [ ] Configure Redis or MemoryCache
- [ ] Create PredictionCacheService
- [ ] Implement GetCachedPrediction method
- [ ] Generate cache key: {playerId}*{gameDate}*{opponent}
- [ ] Check cache before Python call
- [ ] Implement SetCachedPrediction method
- [ ] Set TTL to 1 hour
- [ ] Add cache hit/miss logging
- [ ] Invalidate cache on data ingestion
- [ ] Write unit tests

-----

### Story 5.5: Build Prediction Confidence Scoring

**Story ID**: MBALL-345  
**Story Points**: 5  
**Priority**: Low  
**Acceptance Criteria**:

- Confidence score (0-100) based on model metrics
- Higher confidence for lower MAE models
- Lower confidence for fewer recent games
- Included in prediction response

**Tasks**:

- [ ] Create ConfidenceCalculator class
- [ ] Implement CalculateConfidence method
- [ ] Factor in model MAE
- [ ] Factor in number of recent games available
- [ ] Factor in minutes trend
- [ ] Generate 0-100 score
- [ ] Add Confidence field to PlayerProjectionDto
- [ ] Return confidence with prediction
- [ ] Write unit tests

-----

### Story 5.6: Implement Fallback Prediction Strategy

**Story ID**: MBALL-346  
**Story Points**: 3  
**Priority**: Medium  
**Acceptance Criteria**:

- If model unavailable, use season average
- If season average unavailable, use career average
- Log fallback usage
- Return IsModelPrediction flag

**Tasks**:

- [ ] Extend PythonInferenceService
- [ ] Catch model not found exception
- [ ] Query PlayerSeasonStats for season average
- [ ] If no season stats, query all seasons for career avg
- [ ] Return fallback prediction
- [ ] Add IsModelPrediction boolean to DTO
- [ ] Log fallback reason
- [ ] Write unit tests for fallback

-----

## EPIC 6: TESTING & QUALITY ASSURANCE

**Epic ID**: MBALL-350  
**Story Points**: 21  
**Priority**: High  
**Dependencies**: All previous epics

### Story 6.1: Write Unit Tests for Player Entities

**Story ID**: MBALL-351  
**Story Points**: 5  
**Priority**: High  
**Acceptance Criteria**:

- Tests for all entity classes
- FluentAssertions throughout
- Computed column tests
- Navigation property tests
- 90%+ code coverage

**Tasks**:

- [ ] Create PlayerTests.cs
- [ ] Test Player entity creation
- [ ] Test FullName computed property
- [ ] Create PlayerSeasonStatTests.cs
- [ ] Test PointsReboundsAssists computed property
- [ ] Create PlayerGameStatTests.cs
- [ ] Test IsDoubleDouble computed logic
- [ ] Test IsTripleDouble computed logic
- [ ] Test DraftKingsFantasyScore computed logic
- [ ] Use FluentAssertions Should().Be syntax
- [ ] Add “because” clauses for readability
- [ ] Achieve 90%+ coverage

-----

### Story 6.2: Write Unit Tests for Ingestion Service

**Story ID**: MBALL-352  
**Story Points**: 8  
**Priority**: High  
**Acceptance Criteria**:

- Tests for IngestNBAPlayersAsync
- Tests for IngestPlayerGameStatsAsync
- Idempotency tests (run twice, same result)
- DNP handling tests
- FluentAssertions throughout

**Tasks**:

- [ ] Create PlayerIngestionServiceTests.cs
- [ ] Test IngestNBAPlayersAsync with mock API
- [ ] Verify new players created
- [ ] Verify existing players updated
- [ ] Test idempotency (run twice, check count)
- [ ] Test IngestPlayerGameStatsAsync
- [ ] Verify stats upserted correctly
- [ ] Test DNP handling (DidNotPlay = true)
- [ ] Test minutes parsing
- [ ] Mock repository and API service
- [ ] Use FluentAssertions
- [ ] Verify logging calls

-----

### Story 6.3: Write Unit Tests for Calculators

**Story ID**: MBALL-353  
**Story Points**: 5  
**Priority**: High  
**Acceptance Criteria**:

- Tests for DerivedStatCalculator
- Tests for DraftKingsFantasyCalculator
- Theory tests for multiple scenarios
- Edge case coverage
- FluentAssertions

**Tasks**:

- [ ] Create DerivedStatCalculatorTests.cs
- [ ] Test IsDoubleDouble with Theory attribute
- [ ] Test cases: pts+reb, pts+ast, reb+ast, pts+stl, etc.
- [ ] Test IsTripleDouble with Theory
- [ ] Test CalculateRollingAverage with various N
- [ ] Test empty games list
- [ ] Create DraftKingsFantasyCalculatorTests.cs
- [ ] Test basic scoring without bonuses
- [ ] Test double-double bonus (+1.5)
- [ ] Test triple-double bonus (+3.0, not +1.5)
- [ ] Test turnover penalties
- [ ] Test ProjectFantasyScore with probabilities
- [ ] Use FluentAssertions throughout
- [ ] Add descriptive “because” clauses

-----

### Story 6.4: Write Integration Tests for ML Pipeline

**Story ID**: MBALL-354  
**Story Points**: 8  
**Priority**: Medium  
**Acceptance Criteria**:

- End-to-end test: train and predict
- Test on sample dataset
- Verify model persistence
- Verify predictions reasonable
- Test all model types

**Tasks**:

- [ ] Create MLPipelineIntegrationTests.py
- [ ] Create sample dataset (100 games)
- [ ] Test LinearRegression training
- [ ] Verify model saved to file
- [ ] Test prediction on new data
- [ ] Test RandomForest training
- [ ] Test XGBoost training
- [ ] Test model comparison
- [ ] Verify best model selection
- [ ] Test prediction with best model
- [ ] Assert predictions within reasonable range
- [ ] Clean up test models after

-----

### Story 6.5: Write Integration Tests for Prediction API

**Story ID**: MBALL-355  
**Story Points**: 5  
**Priority**: Medium  
**Acceptance Criteria**:

- Test GetPlayerProjection endpoint
- Test with real player data
- Verify Python script execution
- Test error handling
- Test caching

**Tasks**:

- [ ] Create PredictionsControllerTests.cs
- [ ] Setup test database with sample players
- [ ] Train test model
- [ ] Test GET /api/predictions/player/{id}
- [ ] Verify 200 OK response
- [ ] Verify prediction values present
- [ ] Test with invalid player ID (404)
- [ ] Test when model not found (graceful fallback)
- [ ] Test caching (second call uses cache)
- [ ] Test batch endpoint
- [ ] Clean up test data

-----

## EPIC 7: DOCUMENTATION & DEPLOYMENT

**Epic ID**: MBALL-360  
**Story Points**: 13  
**Priority**: Medium  
**Dependencies**: All previous epics

### Story 7.1: Write Technical Documentation

**Story ID**: MBALL-361  
**Story Points**: 5  
**Priority**: Medium  
**Acceptance Criteria**:

- Database schema documented
- API endpoints documented with examples
- ML pipeline documented
- Feature engineering explained
- Deployment guide

**Tasks**:

- [ ] Create README.md for player layer
- [ ] Document database tables and relationships
- [ ] Document API endpoints
- [ ] Add request/response examples
- [ ] Document ML training process
- [ ] Explain feature engineering logic
- [ ] Document model comparison strategy
- [ ] Write deployment guide
- [ ] Add troubleshooting section
- [ ] Include performance benchmarks

-----

### Story 7.2: Create Database Migration Scripts

**Story ID**: MBALL-362  
**Story Points**: 3  
**Priority**: High  
**Acceptance Criteria**:

- Migration script for all tables
- Rollback script
- Seed data script for testing
- Idempotent - can run multiple times

**Tasks**:

- [ ] Create 001_CreatePlayerTables.sql
- [ ] Create Players table
- [ ] Create PlayerSeasonStats table
- [ ] Create PlayerGameStats table
- [ ] Create MLPlayerModels table
- [ ] Create ML_PlayerFeatures view
- [ ] Add all indexes
- [ ] Create 001_Rollback.sql
- [ ] Drop tables in reverse order
- [ ] Create seed_test_data.sql
- [ ] Test migrations on clean database

-----

### Story 7.3: Setup Python Environment Script

**Story ID**: MBALL-363  
**Story Points**: 3  
**Priority**: Medium  
**Acceptance Criteria**:

- setup_ml_env.sh script
- Creates virtual environment
- Installs all dependencies
- Configures database connection
- Works on Linux and Windows

**Tasks**:

- [ ] Create setup_ml_env.sh
- [ ] Create Python virtual environment
- [ ] Install requirements: scikit-learn, xgboost, lightgbm, pandas, sqlalchemy
- [ ] Create requirements.txt
- [ ] Pin versions for reproducibility
- [ ] Configure database connection string
- [ ] Test on Ubuntu
- [ ] Create setup_ml_env.ps1 for Windows
- [ ] Document environment setup

-----

### Story 7.4: Create Monitoring Dashboard

**Story ID**: MBALL-364  
**Story Points**: 5  
**Priority**: Low  
**Acceptance Criteria**:

- Dashboard shows model performance metrics
- Displays prediction accuracy over time
- Shows ingestion job status
- Alerts on errors

**Tasks**:

- [ ] Create ModelMetricsDashboard.cshtml
- [ ] Query MLPlayerModels table
- [ ] Display MAE, RMSE, R2 for each model
- [ ] Show best model per stat
- [ ] Add chart for prediction accuracy trends
- [ ] Query prediction logs
- [ ] Show ingestion job status
- [ ] Add error alerts
- [ ] Style with Tailwind/Bootstrap

-----

-----

## APPENDIX A: STAT CATEGORIES

**Target Statistics for Modeling:**

1. Points
1. Rebounds
1. Assists
1. ThreePointsMade (3PM)
1. Steals
1. Blocks
1. Turnovers
1. PointsReboundsAssists (PRA)
1. DraftKingsFantasyScore
1. DoubleDouble (probability 0-1)
1. TripleDouble (probability 0-1)

-----

## APPENDIX B: DRAFTKINGS SCORING REFERENCE

**Point Values:**

- Points: 1.0 per point
- 3-Pointers Made: 0.5 per 3PM
- Rebounds: 1.25 per rebound
- Assists: 1.5 per assist
- Steals: 2.0 per steal
- Blocks: 2.0 per block
- Turnovers: -0.5 per turnover

**Bonuses:**

- Double-Double: +1.5 points
- Triple-Double: +3.0 points (replaces double-double bonus)

-----

## APPENDIX C: MODEL TYPES

**Linear Regression:**

- Fast training
- Interpretable coefficients
- Baseline model

**Random Forest:**

- Non-linear relationships
- Feature importance
- Robust to outliers

**XGBoost:**

- High performance
- Handles missing values
- Regularization

**LightGBM:**

- Faster than XGBoost
- Large dataset optimization
- Categorical feature support

-----

## APPENDIX D: FEATURE LIST

**Rolling Averages:**

- Points_L3, Points_L5, Points_L10
- Rebounds_L3, Rebounds_L5, Rebounds_L10
- Assists_L3, Assists_L5, Assists_L10
- ThreePointsMade_L3, ThreePointsMade_L5, ThreePointsMade_L10
- Minutes_L3, Minutes_L5, Minutes_L10

**Season Context:**

- Season_Avg_Points
- Season_Avg_Rebounds
- Season_Avg_Assists
- Season_Avg_Minutes

**Home/Away:**

- Points_HomeAvg
- Points_AwayAvg
- IsHomeGame (boolean)

**Rest:**

- DaysSinceLastGame
- IsBackToBack (boolean)

**Opponent:**

- OpponentTeamId
- OpponentDefense_PointsAllowed
- OpponentDefense_ReboundsAllowed

**Pace:**

- OpponentPace (total score proxy)

**Player Context:**

- Position
- MinutesTrend

-----

## APPENDIX E: SUCCESS METRICS

**Data Quality:**

- 100% of games have complete boxscores
- < 1% missing player stats
- All computed columns accurate

**ML Performance:**

- MAE < 3.0 for Points predictions
- MAE < 2.0 for Rebounds predictions
- MAE < 1.5 for Assists predictions
- R2 > 0.60 for best models

**System Performance:**

- Predictions return < 2 seconds
- Ingestion processes < 100 games/minute
- API uptime > 99%

**Testing:**

- Unit test coverage > 90%
- All integration tests passing
- Zero critical bugs

-----

## VERSION HISTORY

|Version|Date    |Author  |Changes              |
|-------|--------|--------|---------------------|
|1.0    |Feb 2026|Dev Team|Initial specification|

-----

**END OF SPECIFICATION**