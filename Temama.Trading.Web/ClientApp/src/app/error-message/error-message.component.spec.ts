/// <reference path="../../../../node_modules/@types/jasmine/index.d.ts" />
import { TestBed, async, ComponentFixture, ComponentFixtureAutoDetect } from '@angular/core/testing';
import { BrowserModule, By } from "@angular/platform-browser";
import { ErrorMessageComponent } from './error-message.component';

let component: ErrorMessageComponent;
let fixture: ComponentFixture<ErrorMessageComponent>;

describe('error-message component', () => {
    beforeEach(async(() => {
        TestBed.configureTestingModule({
            declarations: [ ErrorMessageComponent ],
            imports: [ BrowserModule ],
            providers: [
                { provide: ComponentFixtureAutoDetect, useValue: true }
            ]
        });
        fixture = TestBed.createComponent(ErrorMessageComponent);
        component = fixture.componentInstance;
    }));

    it('should do something', async(() => {
        expect(true).toEqual(true);
    }));
});