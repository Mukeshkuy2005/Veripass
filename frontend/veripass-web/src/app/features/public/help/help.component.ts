import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';

interface FaqItem {
  id: number;
  question: string;
  answer: string;
  category: string;
}

@Component({
  selector: 'app-help',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './help.component.html',
  styleUrl: './help.component.css'
})
export class HelpComponent {
  searchQuery = '';
  selectedCategory = signal<string | null>(null);
  openFaqId = signal<number | null>(1);

  categories = [
    'Getting Started',
    'Business Passport',
    'Verification',
    'Credentials',
    'Compliance',
    'Buyers',
    'Security',
    'Account'
  ];

  faqs: FaqItem[] = [
    {
      id: 1,
      question: 'What is a Veripass Business Passport?',
      answer: 'A Veripass Business Passport is a unified, tamper-evident digital identity for Indian businesses. It aggregates verified legal identity, statutory tax registrations (GST, PAN, MCA), quality certifications (ISO), and physical address proofs into a single verifiable credential with a dynamic Trust Score.',
      category: 'Business Passport'
    },
    {
      id: 2,
      question: 'How does business verification work?',
      answer: 'Verification follows a 4-tier pipeline: (1) Organization registration, (2) Document upload and cryptographic hashing, (3) Automated OCR extraction and real-time cross-referencing against sovereign government databases (GSTN, MCA21, CBDT), and (4) Final review and certification by licensed human compliance officers.',
      category: 'Verification'
    },
    {
      id: 3,
      question: 'How long does verification take?',
      answer: 'Standard statutory credentials (GST, PAN, CIN) with clear sovereign registry matches are verified within 2 to 4 hours. Complex physical address inspections or non-standard ISO certifications are typically completed within 24 to 48 hours.',
      category: 'Getting Started'
    },
    {
      id: 4,
      question: 'What credentials can I verify?',
      answer: 'Veripass supports GSTIN registration, Permanent Account Number (PAN), MCA Certificate of Incorporation (CIN), MSME Udyam certificates, ISO certifications (via IAF CertSearch), registered physical address utilities, and commercial bank account validity via IMPS penny-drop.',
      category: 'Credentials'
    },
    {
      id: 5,
      question: 'Can I control what buyers see?',
      answer: 'Yes. Suppliers maintain granular custody over their information. Public passports only display verified badges, business category, location, and overall Trust Score. Confidential documents (e.g. detailed bank statements or balance sheets) require explicit supplier approval before access is granted to a buyer.',
      category: 'Business Passport'
    },
    {
      id: 6,
      question: 'How often is my information reverified?',
      answer: 'Veripass runs active background health checks against statutory databases. Tax return currency (GSTR-3B) and operational status are monitored continuously, while major document credentials undergo annual reverification.',
      category: 'Compliance'
    },
    {
      id: 7,
      question: 'What happens when a credential expires?',
      answer: 'When a time-bound credential (such as an ISO 9001 certificate or factory license) enters its 30-day renewal window, automated notifications are sent to the supplier. If not renewed by expiry, the passport status transitions to "Expires Soon" and subsequently reduces the Trust Score until updated proof is filed.',
      category: 'Compliance'
    },
    {
      id: 8,
      question: 'Can I dispute a verification decision?',
      answer: 'Yes. If a credential was rejected or flagged, suppliers can file an appeal directly from their authenticated workspace. Disputes include evidence re-examination and are assigned to a Senior Compliance Officer who did not handle the original review.',
      category: 'Account'
    },
    {
      id: 9,
      question: 'How does Veripass use AI?',
      answer: 'Veripass uses machine learning strictly to accelerate evidence parsing—such as automated OCR, field normalization, and anomaly pattern detection. In accordance with our core safety doctrine ("AI Assists — Humans Decide"), AI never independently makes an irreversible verification approval or rejection.',
      category: 'Security'
    },
    {
      id: 10,
      question: 'How can I contact support?',
      answer: 'You can reach out via our public Contact page, or authenticated users can submit priority tickets directly through their dashboard. Our compliance support desk operates Monday through Saturday, 9:00 AM to 6:00 PM IST.',
      category: 'Getting Started'
    }
  ];

  filteredFaqs = computed(() => {
    let result = this.faqs;
    const cat = this.selectedCategory();
    if (cat) {
      result = result.filter(f => f.category === cat);
    }
    const q = this.searchQuery.trim().toLowerCase();
    if (q) {
      result = result.filter(
        f => f.question.toLowerCase().includes(q) || f.answer.toLowerCase().includes(q) || f.category.toLowerCase().includes(q)
      );
    }
    return result;
  });

  toggleFaq(id: number): void {
    this.openFaqId.update(current => (current === id ? null : id));
  }
}
