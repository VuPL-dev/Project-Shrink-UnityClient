const express = require('express');
const cors = require('cors');
require('dotenv').config();

// Database connection scripts
const { connectPostgres } = require('./config/postgres');
const connectMongo = require('./config/mongo');
const { connectRedis } = require('./config/redis');

const app = express();

// Global Middlewares
app.use(cors()); // CORS middleware
app.use(express.json()); // JSON parser middleware
app.use(express.urlencoded({ extended: true }));

// Basic route
app.get('/api/health', (req, res) => {
  res.status(200).json({ status: 'OK', message: 'Project Shrink Server is running!' });
});

// Error Handling Middleware
app.use((err, req, res, next) => {
  console.error(err.stack);
  res.status(500).json({ error: 'Something went wrong on the server!' });
});

const PORT = process.env.PORT || 3000;

const startServer = async () => {
  // Connect to databases
  await connectPostgres();
  await connectMongo();
  await connectRedis();

  // Sync PostgreSQL tables
  const { syncDatabase } = require('./models/pg');
  await syncDatabase();

  app.listen(PORT, () => {
    console.log(`🚀 Server is running on port ${PORT}`);
  });
};

startServer();
