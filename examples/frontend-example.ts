/**
 * Frontend example: Baking a badge from a Static Web App
 * 
 * This demonstrates how to call the Open Badges API from a TypeScript/JavaScript
 * frontend application (e.g., React, Vue, Angular, or vanilla JS).
 */

interface IssuerData {
  id: string;
  name: string;
  url: string;
  email: string;
  description?: string;
  image?: string;
}

interface BadgeClassData {
  id: string;
  name: string;
  description: string;
  image: string;
  issuer: string;
  criteria: string[];
  tags: string[];
}

interface RecipientData {
  type: string;
  identity: string;
  hashed: boolean;
}

interface AwardData {
  issuer: IssuerData;
  badgeClass: BadgeClassData;
  recipient: RecipientData;
  issuedOn?: string;
  expires?: string;
  evidence?: string;
}

interface BakeRequest {
  standard: string;
  award: AwardData;
}

interface BakeResponse {
  issuerUrl: string;
  badgeClassUrl: string;
  assertionUrl: string;
  bakedPngUrl: string;
}

/**
 * Bakes a badge into a PNG image
 * @param pngFile - The PNG image file to bake the badge into
 * @param awardData - The award data containing issuer, badge class, and recipient info
 * @param functionEndpoint - The Azure Function endpoint URL
 * @param functionKey - The Azure Function key for authentication
 * @returns Promise resolving to the bake response with URLs
 */
async function bakeBadge(
  pngFile: File,
  awardData: AwardData,
  functionEndpoint: string = 'https://your-function-app.azurewebsites.net/api/bake',
  functionKey?: string
): Promise<BakeResponse> {
  
  // Create form data
  const formData = new FormData();
  formData.append('png', pngFile);
  
  const bakeRequest: BakeRequest = {
    standard: 'ob2', // Use 'ob2' for Open Badges 2.0
    award: awardData
  };
  
  formData.append('json', JSON.stringify(bakeRequest));

  // Prepare headers
  const headers: HeadersInit = {};
  if (functionKey) {
    headers['x-functions-key'] = functionKey;
  }

  // Make the request
  const response = await fetch(functionEndpoint, {
    method: 'POST',
    body: formData,
    headers: headers
  });

  if (!response.ok) {
    const error = await response.json();
    throw new Error(`Badge baking failed: ${error.error || response.statusText}`);
  }

  return await response.json() as BakeResponse;
}

/**
 * Downloads a baked badge PNG from the provided URL
 * @param bakedPngUrl - The URL to the baked badge (usually a SAS URL from Azure)
 * @param filename - The desired filename for the download
 */
async function downloadBakedBadge(bakedPngUrl: string, filename: string = 'badge.png'): Promise<void> {
  const response = await fetch(bakedPngUrl);
  const blob = await response.blob();
  
  // Create download link
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  window.URL.revokeObjectURL(url);
}

/**
 * Example usage in a React component or vanilla JS
 */
async function exampleUsage() {
  try {
    // Get the PNG file from a file input
    const fileInput = document.getElementById('badge-template') as HTMLInputElement;
    if (!fileInput.files || fileInput.files.length === 0) {
      console.error('No file selected');
      return;
    }
    
    const pngFile = fileInput.files[0];

    // Prepare award data
    const awardData: AwardData = {
      issuer: {
        id: 'my-organization',
        name: 'My Organization',
        url: 'https://myorg.example.com',
        email: 'badges@myorg.example.com',
        description: 'A leading provider of digital credentials'
      },
      badgeClass: {
        id: 'professional-certification',
        name: 'Professional Certification',
        description: 'Awarded for completing professional certification requirements',
        image: 'https://myorg.example.com/images/cert-badge.png',
        issuer: 'my-organization',
        criteria: [
          'Completed required coursework',
          'Passed certification exam',
          'Demonstrated practical skills'
        ],
        tags: ['certification', 'professional-development']
      },
      recipient: {
        type: 'email',
        identity: 'recipient@example.com',
        hashed: false
      },
      issuedOn: new Date().toISOString(),
      evidence: 'https://myorg.example.com/portfolio/recipient-123'
    };

    // Bake the badge
    console.log('Baking badge...');
    const result = await bakeBadge(
      pngFile,
      awardData,
      'https://your-function-app.azurewebsites.net/api/bake',
      'your-function-key-here' // Optional: Use environment variable in production
    );

    console.log('Badge baked successfully!');
    console.log('Issuer URL:', result.issuerUrl);
    console.log('Badge Class URL:', result.badgeClassUrl);
    console.log('Assertion URL:', result.assertionUrl);
    console.log('Baked PNG URL:', result.bakedPngUrl);

    // Download the baked badge
    await downloadBakedBadge(result.bakedPngUrl, 'my-baked-badge.png');
    
    // Or display it in an <img> tag
    const imgElement = document.getElementById('badge-preview') as HTMLImageElement;
    if (imgElement) {
      imgElement.src = result.bakedPngUrl;
    }

  } catch (error) {
    console.error('Error baking badge:', error);
    alert(`Failed to bake badge: ${error instanceof Error ? error.message : 'Unknown error'}`);
  }
}

/**
 * React component example
 */
interface BadgeBakerProps {
  functionEndpoint: string;
  functionKey?: string;
}

// Uncomment for React usage:
/*
import React, { useState } from 'react';

export const BadgeBaker: React.FC<BadgeBakerProps> = ({ functionEndpoint, functionKey }) => {
  const [pngFile, setPngFile] = useState<File | null>(null);
  const [bakedBadgeUrl, setBakedBadgeUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      setPngFile(e.target.files[0]);
    }
  };

  const handleBakeBadge = async () => {
    if (!pngFile) {
      setError('Please select a PNG file');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const awardData: AwardData = {
        // ... your award data here
      };

      const result = await bakeBadge(pngFile, awardData, functionEndpoint, functionKey);
      setBakedBadgeUrl(result.bakedPngUrl);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to bake badge');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      <h2>Bake Open Badge</h2>
      <input type="file" accept="image/png" onChange={handleFileChange} />
      <button onClick={handleBakeBadge} disabled={loading}>
        {loading ? 'Baking...' : 'Bake Badge'}
      </button>
      {error && <div style={{ color: 'red' }}>{error}</div>}
      {bakedBadgeUrl && (
        <div>
          <h3>Baked Badge</h3>
          <img src={bakedBadgeUrl} alt="Baked badge" />
          <button onClick={() => downloadBakedBadge(bakedBadgeUrl, 'badge.png')}>
            Download
          </button>
        </div>
      )}
    </div>
  );
};
*/

export { bakeBadge, downloadBakedBadge, exampleUsage };
export type { BakeRequest, BakeResponse, AwardData, IssuerData, BadgeClassData, RecipientData };
