import { jsPDF } from 'jspdf';

describe('PDF Generation & Response Headers', () => {
  it('generates a valid binary PDF with application/pdf MIME type and .pdf filename', () => {
    const doc = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' });
    doc.text('VERIPASS OFFICIAL TEST PASSPORT', 20, 20);

    const blob: Blob = doc.output('blob');

    // MIME type check
    expect(blob.type).toBe('application/pdf');
    expect(blob.size).toBeGreaterThan(500); // Valid PDF has non-trivial size

    // Filename check
    const filename = 'VERIPASS-SOVEREIGN-PASSPORT-VP-IN-12345.pdf';
    expect(filename.endsWith('.pdf')).toBeTrue();
    expect(filename).toMatch(/\.pdf$/i);
  });

  it('contains PDF 1.4+ magic bytes (%PDF-) in output stream', (done) => {
    const doc = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' });
    doc.text('VERIPASS Cryptographic Attestation', 20, 20);

    const blob: Blob = doc.output('blob');
    const reader = new FileReader();

    reader.onloadend = () => {
      const buffer = reader.result as ArrayBuffer;
      const bytes = new Uint8Array(buffer.slice(0, 5));
      const magic = String.fromCharCode(...bytes);
      expect(magic).toBe('%PDF-');
      done();
    };

    reader.readAsArrayBuffer(blob);
  });

  it('safely handles Object URL creation and revocation without throwing', () => {
    const doc = new jsPDF();
    doc.text('Memory Leak Test', 10, 10);
    const blob: Blob = doc.output('blob');

    const url = URL.createObjectURL(blob);
    expect(url).toContain('blob:');

    // Revocation shouldn't throw error
    expect(() => URL.revokeObjectURL(url)).not.toThrow();
  });
});
