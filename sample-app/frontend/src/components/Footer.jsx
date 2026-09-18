import React from 'react';

function Footer() {
  return (
    <footer className="footer">
      <div className="footer-content">
        <span className="footer-brand">Mission Control v0.1</span>
        <span className="footer-separator">|</span>
        <span className="footer-status">API: <span className="footer-api-status">Unknown</span></span>
        <span className="footer-separator">|</span>
        <span className="footer-tests">Tests: 0 passing (0% coverage)</span>
      </div>
    </footer>
  );
}

export default Footer;
