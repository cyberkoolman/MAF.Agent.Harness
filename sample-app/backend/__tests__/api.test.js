const request = require('supertest');
const app = require('../server');

describe('API Tests', () => {

  test('GET /api/health -> 200, status ok and uptime', async () => {
    const response = await request(app).get('/api/health');
    expect(response.status).toBe(200);
    expect(response.body.status).toBe('ok');
    expect(typeof response.body.uptime).toBe('number');
  });

  test('GET /api/users -> 200, response is an array', async () => {
    const response = await request(app).get('/api/users');
    expect(response.status).toBe(200);
    expect(Array.isArray(response.body)).toBe(true);
  });

  test('GET /api/users/:id (valid) -> 200, user data', async () => {
    const response = await request(app).get('/api/users/1'); // Assuming user with id 1 exists in the database
    expect(response.status).toBe(200);
    expect(response.body).toHaveProperty('id', 1);
    expect(response.body).toHaveProperty('name');
    expect(response.body).toHaveProperty('role');
  });

  test('GET /api/users/:id (invalid) -> 404, user not found', async () => {
    const response = await request(app).get('/api/users/999');
    expect(response.status).toBe(404);
    expect(response.body).toHaveProperty('error', 'User not found');
  });

  test('GET /api/users/:id/stats (valid) -> 200, user statistics', async () => {
    const response = await request(app).get('/api/users/1/stats'); // Assuming user with id 1 exists in the database
    expect(response.status).toBe(200);
    expect(response.body).toHaveProperty('totalTasks');
    expect(response.body).toHaveProperty('completedTasks');
    expect(response.body).toHaveProperty('completionRate');
  });

  test('GET /api/users/:id/stats (invalid) -> 404, user not found', async () => {
    const response = await request(app).get('/api/users/999/stats');
    expect(response.status).toBe(404);
    expect(response.body).toHaveProperty('error', 'User not found');
  });

  test('GET /api/tasks -> 200, response is an array', async () => {
    const response = await request(app).get('/api/tasks');
    expect(response.status).toBe(200);
    expect(Array.isArray(response.body)).toBe(true);
  });

  test('GET /api/tasks/:id (valid) -> 200, task data', async () => {
    const response = await request(app).get('/api/tasks/1'); // Assuming task with id 1 exists
    expect(response.status).toBe(200);
    expect(response.body).toHaveProperty('id', 1);
    expect(response.body).toHaveProperty('title');
    expect(response.body).toHaveProperty('status');
  });

  test('GET /api/tasks/:id (invalid) -> 404, task not found', async () => {
    const response = await request(app).get('/api/tasks/999');
    expect(response.status).toBe(404);
    expect(response.body).toHaveProperty('error');
  });

});