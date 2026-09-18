const express = require('express');
const app = express();

app.use(express.json());

// Health endpoint
app.get('/api/health', (req, res) => {
  res.json({ status: 'ok' });
});

// Users routes
const usersRouter = require('./routes/users');
app.use('/api/users', usersRouter);

module.exports = app;
